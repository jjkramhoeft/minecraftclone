using Microsoft.Xna.Framework;

namespace MinecraftClone.World;

/// <summary>
/// Fills chunks with terrain derived deterministically from a single seed.
/// Phase 2: rolling hills via fBm noise — stone core, dirt layer, grass top.
/// </summary>
public class TerrainGenerator
{
    private const int BaseHeight = 44;
    private const float Amplitude = 16f;
    private const int DirtDepth = 3;

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
        for (int x = 0; x < Chunk.SizeX; x++)
        {
            for (int z = 0; z < Chunk.SizeZ; z++)
            {
                int worldX = chunk.Coord.X * Chunk.SizeX + x;
                int worldZ = chunk.Coord.Z * Chunk.SizeZ + z;

                float noise = _heightNoise.GetNoise(worldX, worldZ); // [-1, 1]
                int height = (int)MathHelper.Clamp(BaseHeight + noise * Amplitude, 1, Chunk.SizeY - 1);

                for (int y = 0; y <= height; y++)
                {
                    BlockType type =
                        y == height ? BlockType.Grass :
                        y >= height - DirtDepth ? BlockType.Dirt :
                        BlockType.Stone;
                    chunk.SetBlock(x, y, z, type);
                }
            }
        }
    }
}
