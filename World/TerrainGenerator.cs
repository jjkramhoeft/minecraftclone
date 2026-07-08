using System;
using Microsoft.Xna.Framework;

namespace MinecraftClone.World;

public enum Biome : byte
{
    Desert,
    Plains,
    Forest,
    Mountains,
    Lake,
}

/// <summary>
/// Fills chunks with terrain derived deterministically from a single seed:
/// fBm-noise hills (stone core, dirt layer, grass top), sandy lowlands,
/// biome-dependent vegetation, and carved caves.
/// </summary>
public class TerrainGenerator
{
    private const int BaseHeight = 44;
    private const int DirtDepth = 3;
    private const int SandLevel = 38;       // surfaces at or below this are sandy
    private const int WaterLevel = 37;      // valleys below this fill with water
    private const int ReedChance = 3;       // reeds on ~1/3 of eligible shoreline columns

    // Mountain biome: forest's peak amplitude is 24, so +12 gives ~150% taller
    // peaks. The lift raises the whole biome so even the slopes stand above the
    // surrounding forest. Above RockLine the surface is bare stone (no trees);
    // at or above SnowLine it is capped with snow.
    private const float MountainAmplitudeBoost = 12f;
    private const int MountainLift = 6;
    // Tuned against the actual height distribution (mountain peaks reach ~80 but
    // taper off sharply above ~62): RockLine leaves ~10% of mountain columns as
    // bare summit, SnowLine caps the tallest ~3%. Higher values made snow so
    // rare it was effectively never seen in-game.
    private const int RockLine = 58;
    private const int SnowLine = 63;
    private const int MaxSnowCap = 4;   // snow thickens toward the peak, up to this many blocks

    // Lake biome: a separate low-frequency field carves broad basins that dip
    // well below the water level, giving larger, deeper water than the ordinary
    // valley lakes. The depression is smooth (bowl-shaped) so shores taper in.
    private const int MaxLakeDepth = 16;

    private const int BedrockDepth = 2;     // never carve at or below this — fake bedrock
    private const float CaveThreshold = 0.2f; // tunnel radius: both noise fields within ±this (original value 0.09f)

    private readonly FastNoiseLite _heightNoise;
    private readonly FastNoiseLite _biomeNoise;
    private readonly FastNoiseLite _lakeNoise;
    private readonly FastNoiseLite _caveNoiseA;
    private readonly FastNoiseLite _caveNoiseB;

    public int Seed { get; }

    public TerrainGenerator(int seed)
    {
        Seed = seed;
        _heightNoise = new FastNoiseLite(seed);
        _heightNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        _heightNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
        _heightNoise.SetFractalOctaves(4);
        _heightNoise.SetFrequency(0.008f);

        // One very low-frequency field drives both biome choice and terrain
        // amplitude, so deserts come out flat and forests mountainous with no
        // height cliffs at biome borders.
        _biomeNoise = new FastNoiseLite(seed ^ 0x517CC1B7);
        _biomeNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        _biomeNoise.SetFrequency(0.0018f);

        // Independent low-frequency field for lake basins, so lakes can appear
        // within any land biome rather than tracking the temperature field.
        _lakeNoise = new FastNoiseLite(seed ^ 0x63C8A17F);
        _lakeNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        _lakeNoise.SetFrequency(0.0035f);

        // "Spaghetti" caves: a cell is carved where BOTH 3D fields sit near
        // zero, which traces winding tubes instead of open blobs.
        _caveNoiseA = new FastNoiseLite(seed ^ 0x1B873593);
        _caveNoiseA.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        _caveNoiseA.SetFrequency(0.045f);
        _caveNoiseB = new FastNoiseLite(seed ^ unchecked((int)0xCC9E2D51));
        _caveNoiseB.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        _caveNoiseB.SetFrequency(0.045f);
    }

    public void Generate(Chunk chunk)
    {
        Span<int> heights = stackalloc int[Chunk.SizeX * Chunk.SizeZ];
        Span<byte> biomes = stackalloc byte[Chunk.SizeX * Chunk.SizeZ];

        for (int x = 0; x < Chunk.SizeX; x++)
        {
            for (int z = 0; z < Chunk.SizeZ; z++)
            {
                int worldX = chunk.Coord.X * Chunk.SizeX + x;
                int worldZ = chunk.Coord.Z * Chunk.SizeZ + z;

                ClassifyColumn(worldX, worldZ, out int height, out Biome biome);
                heights[x + z * Chunk.SizeX] = height;
                biomes[x + z * Chunk.SizeX] = (byte)biome;

                // Bare rocky summits above the treeline, snow-capped near the
                // peak. The cap thickens with elevation so tall peaks wear a
                // clear white crown rather than a single sliver. Lake columns
                // sit below the water line, so they never read rocky.
                bool rocky = biome == Biome.Mountains && height >= RockLine;
                int snowCap = rocky && height >= SnowLine
                    ? Math.Min(MaxSnowCap, height - SnowLine + 1) : 0;
                bool sandy = height <= SandLevel || biome == Biome.Desert;

                for (int y = 0; y <= height; y++)
                {
                    BlockType type =
                        rocky ? (y > height - snowCap ? BlockType.Snow : BlockType.Stone) :
                        y < height - DirtDepth ? BlockType.Stone :
                        sandy ? BlockType.Sand :
                        y == height ? BlockType.Grass :
                        BlockType.Dirt;
                    chunk.SetBlock(x, y, z, type);
                }

                // Still water fills the valleys (no flow simulation).
                for (int y = height + 1; y <= WaterLevel; y++)
                    chunk.SetBlock(x, y, z, BlockType.Water);

                CarveCaves(chunk, x, z, worldX, worldZ, height);
            }
        }

        PlaceOres(chunk);
        PlantTrees(chunk, heights, biomes);
        ScatterFlowers(chunk, heights, biomes);
        GrowReeds(chunk, heights);
    }

    /// <summary>The biome a world column ends up in — the same classification the
    /// generator uses, exposed for the F3 biome-frequency readout.</summary>
    public Biome GetBiome(int worldX, int worldZ)
    {
        ClassifyColumn(worldX, worldZ, out _, out var biome);
        return biome;
    }

    /// <summary>Derives a column's surface height and biome from the noise fields.
    /// The single source of truth for both — Generate and GetBiome share it, so
    /// the debug readout can never drift from what was actually placed.</summary>
    private void ClassifyColumn(int worldX, int worldZ, out int height, out Biome biome)
    {
        float biomeValue = _biomeNoise.GetNoise(worldX, worldZ); // [-1, 1]
        biome =
            biomeValue > 0.35f ? Biome.Desert :
            biomeValue < -0.55f ? Biome.Mountains :
            biomeValue < -0.15f ? Biome.Forest :
            Biome.Plains;

        // Deserts (high biomeValue) are flat, forests mountainous.
        float amplitude = MathHelper.Lerp(24f, 9f, (biomeValue + 1f) * 0.5f);
        float lift = 0f;
        if (biome == Biome.Mountains)
        {
            // Ramp 0->1 across the mountain band (-0.55 .. -1) so the boost
            // fades in at the forest border with no height cliff.
            float t = MathHelper.Clamp((-0.55f - biomeValue) / 0.45f, 0f, 1f);
            amplitude += t * MountainAmplitudeBoost;
            lift = t * MountainLift;
        }

        // Lake basins depress the land in a smooth bowl (0 at the rim to
        // MaxLakeDepth at the center), independent of the land biome.
        float lakeFactor = SmoothStep01(0.25f, 0.55f, _lakeNoise.GetNoise(worldX, worldZ));

        float noise = _heightNoise.GetNoise(worldX, worldZ); // [-1, 1]
        height = (int)MathHelper.Clamp(
            BaseHeight + lift + noise * amplitude - lakeFactor * MaxLakeDepth, 1, Chunk.SizeY - 1);

        // A depressed column that ended up under water is lake bed; the shore
        // ring keeps its land biome so vegetation transitions.
        if (lakeFactor > 0.25f && height < WaterLevel)
            biome = Biome.Lake;
    }

    /// <summary>1 tree per ~N eligible columns; 0 = no trees in this biome.</summary>
    private static int TreeSpacingFor(Biome biome) => biome switch
    {
        Biome.Forest => 19,
        Biome.Mountains => 27,  // sparser pines on the lower, grassy slopes
        Biome.Plains => 149,
        _ => 0,
    };

    /// <summary>1 flower per ~N grass columns; 0 = none.</summary>
    private static int FlowerSpacingFor(Biome biome) => biome switch
    {
        Biome.Plains => 11,
        Biome.Forest => 29,
        _ => 0,
    };

    /// <summary>
    /// Depth-banded ore blobs, seeded per chunk so placement is stable across
    /// runs. Blobs only replace stone, so they never poke out of hillsides or
    /// into caves. Coal is common and shallow; iron is rarer and deep.
    /// </summary>
    private void PlaceOres(Chunk chunk)
    {
        var rng = new Random(Hash(chunk.Coord.X, chunk.Coord.Z, Seed ^ 0x0AB1C4D3));
        PlaceOreBlobs(chunk, rng, BlockType.CoalOre, count: 8, minY: 5, maxY: 52);
        PlaceOreBlobs(chunk, rng, BlockType.IronOre, count: 5, minY: 5, maxY: 30);
    }

    private static void PlaceOreBlobs(Chunk chunk, Random rng, BlockType ore, int count, int minY, int maxY)
    {
        for (int blob = 0; blob < count; blob++)
        {
            int cx = rng.Next(Chunk.SizeX);
            int cy = rng.Next(minY, maxY + 1);
            int cz = rng.Next(Chunk.SizeZ);
            int size = rng.Next(4, 10); // blocks per blob (attempted)

            for (int i = 0; i < size; i++)
            {
                int x = cx + rng.Next(-1, 2);
                int y = cy + rng.Next(-1, 2);
                int z = cz + rng.Next(-1, 2);
                if (Chunk.InBounds(x, y, z) && chunk.GetBlock(x, y, z) == BlockType.Stone)
                    chunk.SetBlock(x, y, z, ore);
            }
        }
    }

    /// <summary>
    /// Carves tunnel caves through one column. On land the tunnels may break
    /// the surface (cave entrances); under water the top three blocks of the
    /// floor are kept so the ocean never drains into the cave system.
    /// </summary>
    private void CarveCaves(Chunk chunk, int x, int z, int worldX, int worldZ, int height)
    {
        bool submerged = height < WaterLevel;
        int carveTop = submerged ? height - 3 : height;

        for (int y = BedrockDepth + 1; y <= carveTop; y++)
        {
            float a = _caveNoiseA.GetNoise(worldX, y * 1.6f, worldZ); // stretch y: flatter, longer tunnels
            if (a < -CaveThreshold || a > CaveThreshold)
                continue;
            float b = _caveNoiseB.GetNoise(worldX, y * 1.6f, worldZ);
            if (b < -CaveThreshold || b > CaveThreshold)
                continue;
            chunk.SetBlock(x, y, z, BlockType.Air);
        }
    }

    /// <summary>
    /// Reed beds along shorelines: 2-3 tall stacks on sand/dirt/grass columns
    /// that have water directly beside the supporting block. Water adjacency
    /// is only checkable inside the chunk, so border columns stay bare —
    /// invisible in practice since shorelines meander.
    /// </summary>
    private void GrowReeds(Chunk chunk, ReadOnlySpan<int> heights)
    {
        for (int x = 1; x < Chunk.SizeX - 1; x++)
        {
            for (int z = 1; z < Chunk.SizeZ - 1; z++)
            {
                int worldX = chunk.Coord.X * Chunk.SizeX + x;
                int worldZ = chunk.Coord.Z * Chunk.SizeZ + z;
                int hash = Hash(worldX, worldZ, Seed ^ 0x2E8BA2F1);
                if (hash % ReedChance != 0)
                    continue;

                int surface = heights[x + z * Chunk.SizeX];
                var ground = chunk.GetBlock(x, surface, z);
                if (ground is not (BlockType.Sand or BlockType.Dirt or BlockType.Grass))
                    continue;
                if (surface + 1 >= Chunk.SizeY || chunk.GetBlock(x, surface + 1, z) != BlockType.Air)
                    continue;

                bool waterBeside =
                    chunk.GetBlock(x + 1, surface, z) == BlockType.Water
                    || chunk.GetBlock(x - 1, surface, z) == BlockType.Water
                    || chunk.GetBlock(x, surface, z + 1) == BlockType.Water
                    || chunk.GetBlock(x, surface, z - 1) == BlockType.Water;
                if (!waterBeside)
                    continue;

                int stalkHeight = 2 + hash / ReedChance % 2; // 2-3
                for (int dy = 1; dy <= stalkHeight && surface + dy < Chunk.SizeY; dy++)
                    chunk.SetBlock(x, surface + dy, z, BlockType.Reeds);
            }
        }
    }

    /// <summary>Flowers on grass wherever the salted column hash says so —
    /// runs after trees, so columns under a canopy (leaves above) are skipped.</summary>
    private void ScatterFlowers(Chunk chunk, ReadOnlySpan<int> heights, ReadOnlySpan<byte> biomes)
    {
        for (int x = 0; x < Chunk.SizeX; x++)
        {
            for (int z = 0; z < Chunk.SizeZ; z++)
            {
                int spacing = FlowerSpacingFor((Biome)biomes[x + z * Chunk.SizeX]);
                if (spacing == 0)
                    continue;
                int worldX = chunk.Coord.X * Chunk.SizeX + x;
                int worldZ = chunk.Coord.Z * Chunk.SizeZ + z;
                int hash = Hash(worldX, worldZ, Seed ^ 0x5F375A86);
                if (hash % spacing != 0)
                    continue;

                int surface = heights[x + z * Chunk.SizeX];
                if (surface + 1 >= Chunk.SizeY
                    || chunk.GetBlock(x, surface, z) != BlockType.Grass
                    || chunk.GetBlock(x, surface + 1, z) != BlockType.Air)
                    continue;

                var flower = (BlockType)((byte)BlockType.FlowerRed + hash / spacing % 3);
                chunk.SetBlock(x, surface + 1, z, flower);
            }
        }
    }

    private enum TreeSpecies { Oak, Birch, Pine }

    /// <summary>
    /// Trees are planted only where the whole canopy (radius 2) fits inside the
    /// chunk, so no tree ever straddles a chunk border. Placement and species
    /// come from a deterministic hash of the world column, so they're stable
    /// across runs. Birch (~5%) and pine (~10%) sprinkle in among the oaks.
    /// </summary>
    private void PlantTrees(Chunk chunk, ReadOnlySpan<int> heights, ReadOnlySpan<byte> biomes)
    {
        for (int x = 2; x < Chunk.SizeX - 2; x++)
        {
            for (int z = 2; z < Chunk.SizeZ - 2; z++)
            {
                Biome biome = (Biome)biomes[x + z * Chunk.SizeX];
                int spacing = TreeSpacingFor(biome);
                if (spacing == 0)
                    continue;
                int worldX = chunk.Coord.X * Chunk.SizeX + x;
                int worldZ = chunk.Coord.Z * Chunk.SizeZ + z;
                int hash = Hash(worldX, worldZ, Seed);
                if (hash % spacing != 0)
                    continue;

                int surface = heights[x + z * Chunk.SizeX];
                if (chunk.GetBlock(x, surface, z) != BlockType.Grass)
                    continue;

                // Mountains grow pines only; elsewhere birch (~5%) and pine
                // (~10%) sprinkle in among the oaks. Species from a separately-
                // salted hash so it doesn't correlate with the trunk-height bits.
                TreeSpecies species;
                if (biome == Biome.Mountains)
                    species = TreeSpecies.Pine;
                else
                {
                    int speciesRoll = Hash(worldX, worldZ, Seed ^ 0x6D5A4C3B) % 100;
                    species = speciesRoll < 5 ? TreeSpecies.Birch
                        : speciesRoll < 15 ? TreeSpecies.Pine
                        : TreeSpecies.Oak;
                }

                if (species == TreeSpecies.Pine)
                {
                    PlantPine(chunk, x, z, surface, trunkHeight: 6 + (hash / spacing) % 3); // 6-8
                    ScatterFerns(chunk, heights, x, z);
                }
                else
                    PlantBroadleaf(chunk, x, z, surface, trunkHeight: 4 + (hash / spacing) % 3, // 4-6
                        log: species == TreeSpecies.Birch ? BlockType.BirchLog : BlockType.Wood,
                        leaf: species == TreeSpecies.Birch ? BlockType.BirchLeaves : BlockType.Leaves);
            }
        }
    }

    /// <summary>Oak/birch canopy: two 5x5 leaf layers (minus corners), a 3x3,
    /// and a plus-shape cap over a straight trunk.</summary>
    private static void PlantBroadleaf(Chunk chunk, int x, int z, int surface, int trunkHeight, BlockType log, BlockType leaf)
    {
        int topY = surface + trunkHeight;
        if (topY + 2 >= Chunk.SizeY)
            return;

        for (int y = topY - 1; y <= topY; y++)
            for (int dx = -2; dx <= 2; dx++)
                for (int dz = -2; dz <= 2; dz++)
                    if (!(Math.Abs(dx) == 2 && Math.Abs(dz) == 2))
                        SetIfAir(chunk, x + dx, y, z + dz, leaf);

        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                SetIfAir(chunk, x + dx, topY + 1, z + dz, leaf);

        SetIfAir(chunk, x, topY + 2, z, leaf);
        SetIfAir(chunk, x + 1, topY + 2, z, leaf);
        SetIfAir(chunk, x - 1, topY + 2, z, leaf);
        SetIfAir(chunk, x, topY + 2, z + 1, leaf);
        SetIfAir(chunk, x, topY + 2, z - 1, leaf);

        for (int y = surface + 1; y <= topY; y++)
            chunk.SetBlock(x, y, z, log);
    }

    // Needle-ring radii from the tip downward: alternating wide/narrow tiers
    // give the classic spiky conifer silhouette. Max radius 2 keeps the canopy
    // inside the planted column's 2-block border.
    private static readonly int[] PineRingRadii = { 1, 1, 2, 1, 2, 2 };

    /// <summary>Pine: a single-block leafy tip above tiered needle rings on a
    /// bare-based trunk.</summary>
    private static void PlantPine(Chunk chunk, int x, int z, int surface, int trunkHeight)
    {
        int topY = surface + trunkHeight;
        if (topY + 1 >= Chunk.SizeY)
            return;

        SetIfAir(chunk, x, topY + 1, z, BlockType.PineLeaves); // tip

        for (int layer = 0; layer < PineRingRadii.Length; layer++)
        {
            int y = topY - layer;
            if (y <= surface)
                break;
            int r = PineRingRadii[layer];
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                    if (r < 2 || Math.Abs(dx) != 2 || Math.Abs(dz) != 2) // round the wide tiers
                        SetIfAir(chunk, x + dx, y, z + dz, BlockType.PineLeaves);
        }

        for (int y = surface + 1; y <= topY; y++)
            chunk.SetBlock(x, y, z, BlockType.PineLog);
    }

    /// <summary>Ferns as pine-forest ground cover: 2-4 on grass columns within
    /// radius 2 of the trunk (that reach keeps them inside the planted column's
    /// border, so no bounds surprises). Deterministic and allocation-free.</summary>
    private void ScatterFerns(Chunk chunk, ReadOnlySpan<int> heights, int x, int z)
    {
        int worldX = chunk.Coord.X * Chunk.SizeX + x;
        int worldZ = chunk.Coord.Z * Chunk.SizeZ + z;
        int seed = Hash(worldX, worldZ, Seed ^ 0x3C2B1A09);
        int count = 2 + seed % 3; // 2-4
        for (int i = 0; i < count; i++)
        {
            seed = Hash(seed, i, Seed ^ 0x77CC33AB);
            int dx = seed % 5 - 2;         // -2..2
            int dz = seed / 5 % 5 - 2;     // -2..2
            if (dx == 0 && dz == 0)
                continue; // the trunk column
            int nx = x + dx, nz = z + dz;
            if (nx < 0 || nx >= Chunk.SizeX || nz < 0 || nz >= Chunk.SizeZ)
                continue;
            int surface = heights[nx + nz * Chunk.SizeX];
            if (surface + 1 >= Chunk.SizeY
                || chunk.GetBlock(nx, surface, nz) != BlockType.Grass
                || chunk.GetBlock(nx, surface + 1, nz) != BlockType.Air)
                continue;
            chunk.SetBlock(nx, surface + 1, nz, BlockType.Fern);
        }
    }

    private static void SetIfAir(Chunk chunk, int x, int y, int z, BlockType type)
    {
        if (Chunk.InBounds(x, y, z) && chunk.GetBlock(x, y, z) == BlockType.Air)
            chunk.SetBlock(x, y, z, type);
    }

    /// <summary>Hermite smoothstep: 0 below edge0, 1 above edge1, eased between.</summary>
    private static float SmoothStep01(float edge0, float edge1, float x)
    {
        float t = MathHelper.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    // Stable across runs (unlike HashCode.Combine), which saves depend on.
    private static int Hash(int x, int z, int seed)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)x * 0x9E3779B9u;
            h = (h ^ (h >> 15)) * 0x85EBCA6Bu;
            h ^= (uint)z * 0xC2B2AE35u;
            h = (h ^ (h >> 13)) * 0x27D4EB2Fu;
            return (int)((h ^ (h >> 16)) & 0x7FFFFFFF);
        }
    }
}
