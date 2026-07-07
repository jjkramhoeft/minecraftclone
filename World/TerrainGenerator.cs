using System;
using Microsoft.Xna.Framework;

namespace MinecraftClone.World;

/// <summary>
/// Fills chunks with terrain derived deterministically from a single seed:
/// fBm-noise hills (stone core, dirt layer, grass top), sandy lowlands, and
/// scattered trees.
/// </summary>
public class TerrainGenerator
{
    private const int BaseHeight = 44;
    private const float Amplitude = 16f;
    private const int DirtDepth = 3;
    private const int SandLevel = 38;       // surfaces at or below this are sandy
    private const int WaterLevel = 37;      // valleys below this fill with water
    private const int TreeSpacing = 61;     // 1 tree per ~61 eligible columns
    private const int FlowerSpacing = 17;   // 1 flower per ~17 grass columns

    private readonly FastNoiseLite _heightNoise;

    public int Seed { get; }

    public TerrainGenerator(int seed)
    {
        Seed = seed;
        _heightNoise = new FastNoiseLite(seed);
        _heightNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        _heightNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
        _heightNoise.SetFractalOctaves(4);
        _heightNoise.SetFrequency(0.008f);
    }

    public void Generate(Chunk chunk)
    {
        Span<int> heights = stackalloc int[Chunk.SizeX * Chunk.SizeZ];

        for (int x = 0; x < Chunk.SizeX; x++)
        {
            for (int z = 0; z < Chunk.SizeZ; z++)
            {
                int worldX = chunk.Coord.X * Chunk.SizeX + x;
                int worldZ = chunk.Coord.Z * Chunk.SizeZ + z;

                float noise = _heightNoise.GetNoise(worldX, worldZ); // [-1, 1]
                int height = (int)MathHelper.Clamp(BaseHeight + noise * Amplitude, 1, Chunk.SizeY - 1);
                heights[x + z * Chunk.SizeX] = height;
                bool sandy = height <= SandLevel;

                for (int y = 0; y <= height; y++)
                {
                    BlockType type =
                        y < height - DirtDepth ? BlockType.Stone :
                        sandy ? BlockType.Sand :
                        y == height ? BlockType.Grass :
                        BlockType.Dirt;
                    chunk.SetBlock(x, y, z, type);
                }

                // Still water fills the valleys (no flow simulation).
                for (int y = height + 1; y <= WaterLevel; y++)
                    chunk.SetBlock(x, y, z, BlockType.Water);
            }
        }

        PlantTrees(chunk, heights);
        ScatterFlowers(chunk, heights);
    }

    /// <summary>Flowers on grass wherever the salted column hash says so —
    /// runs after trees, so columns under a canopy (leaves above) are skipped.</summary>
    private void ScatterFlowers(Chunk chunk, ReadOnlySpan<int> heights)
    {
        for (int x = 0; x < Chunk.SizeX; x++)
        {
            for (int z = 0; z < Chunk.SizeZ; z++)
            {
                int worldX = chunk.Coord.X * Chunk.SizeX + x;
                int worldZ = chunk.Coord.Z * Chunk.SizeZ + z;
                int hash = Hash(worldX, worldZ, Seed ^ 0x5F375A86);
                if (hash % FlowerSpacing != 0)
                    continue;

                int surface = heights[x + z * Chunk.SizeX];
                if (surface + 1 >= Chunk.SizeY
                    || chunk.GetBlock(x, surface, z) != BlockType.Grass
                    || chunk.GetBlock(x, surface + 1, z) != BlockType.Air)
                    continue;

                var flower = (BlockType)((byte)BlockType.FlowerRed + hash / FlowerSpacing % 3);
                chunk.SetBlock(x, surface + 1, z, flower);
            }
        }
    }

    /// <summary>
    /// Trees are planted only where the whole canopy (radius 2) fits inside the
    /// chunk, so no tree ever straddles a chunk border. Placement comes from a
    /// deterministic hash of the world column, so it's stable across runs.
    /// </summary>
    private void PlantTrees(Chunk chunk, ReadOnlySpan<int> heights)
    {
        for (int x = 2; x < Chunk.SizeX - 2; x++)
        {
            for (int z = 2; z < Chunk.SizeZ - 2; z++)
            {
                int worldX = chunk.Coord.X * Chunk.SizeX + x;
                int worldZ = chunk.Coord.Z * Chunk.SizeZ + z;
                int hash = Hash(worldX, worldZ, Seed);
                if (hash % TreeSpacing != 0)
                    continue;

                int surface = heights[x + z * Chunk.SizeX];
                if (chunk.GetBlock(x, surface, z) != BlockType.Grass)
                    continue;

                int trunkHeight = 4 + (hash / TreeSpacing) % 3; // 4-6
                int topY = surface + trunkHeight;
                if (topY + 2 >= Chunk.SizeY)
                    continue;

                // Canopy: two 5x5 layers (minus corners), one 3x3, one plus-shape cap.
                for (int y = topY - 1; y <= topY; y++)
                    for (int dx = -2; dx <= 2; dx++)
                        for (int dz = -2; dz <= 2; dz++)
                            if (!(Math.Abs(dx) == 2 && Math.Abs(dz) == 2))
                                SetIfAir(chunk, x + dx, y, z + dz, BlockType.Leaves);

                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                        SetIfAir(chunk, x + dx, topY + 1, z + dz, BlockType.Leaves);

                SetIfAir(chunk, x, topY + 2, z, BlockType.Leaves);
                SetIfAir(chunk, x + 1, topY + 2, z, BlockType.Leaves);
                SetIfAir(chunk, x - 1, topY + 2, z, BlockType.Leaves);
                SetIfAir(chunk, x, topY + 2, z + 1, BlockType.Leaves);
                SetIfAir(chunk, x, topY + 2, z - 1, BlockType.Leaves);

                for (int y = surface + 1; y <= topY; y++)
                    chunk.SetBlock(x, y, z, BlockType.Wood);
            }
        }
    }

    private static void SetIfAir(Chunk chunk, int x, int y, int z, BlockType type)
    {
        if (Chunk.InBounds(x, y, z) && chunk.GetBlock(x, y, z) == BlockType.Air)
            chunk.SetBlock(x, y, z, type);
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
