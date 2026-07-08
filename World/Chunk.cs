using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MinecraftClone.World;

/// <summary>
/// A 16x128x16 column of blocks stored as a flat byte array (32 KB).
/// Dumb data: all orchestration lives in ChunkManager.
/// </summary>
public class Chunk
{
    public const int SizeX = 16;
    public const int SizeY = 128;
    public const int SizeZ = 16;

    public ChunkCoord Coord { get; }

    /// <summary>True once the player has edited this chunk — only modified chunks are saved.</summary>
    public bool IsModified { get; set; }

    /// <summary>True when the block data changed and the mesh needs rebuilding.</summary>
    public bool MeshDirty { get; set; }

    /// <summary>
    /// Bumped on every player edit. Background mesh results carry the version
    /// they were built from, so a stale result never overwrites a newer mesh.
    /// </summary>
    public int Version { get; set; }

    private readonly byte[] _blocks = new byte[SizeX * SizeY * SizeZ];
    private readonly byte[] _light = new byte[SizeX * SizeY * SizeZ];
    private readonly byte[] _skyLight = new byte[SizeX * SizeY * SizeZ];

    /// <summary>Raw block storage — exposed for save/load serialization only.</summary>
    public byte[] Blocks => _blocks;

    /// <summary>Positions of light-emitting blocks in this chunk, maintained by
    /// ChunkManager so cross-chunk light can be reseeded without full rescans.
    /// Filled by the load/generate worker, then owned by the main thread.</summary>
    public List<(byte X, byte Y, byte Z)> Emitters { get; } = new();

    public Chunk(ChunkCoord coord) => Coord = coord;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BlockType GetBlock(int x, int y, int z) => (BlockType)_blocks[Index(x, y, z)];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(int x, int y, int z, BlockType type) => _blocks[Index(x, y, z)] = (byte)type;

    /// <summary>Block light 0-15, derived at runtime from emitters — never saved.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetLight(int x, int y, int z) => _light[Index(x, y, z)];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetLight(int x, int y, int z, byte level) => _light[Index(x, y, z)] = level;

    /// <summary>Sky light 0-15, derived at runtime from the block column (sun
    /// straight down, stopped by the first opaque block) — never saved.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetSkyLight(int x, int y, int z) => _skyLight[Index(x, y, z)];

    /// <summary>Recomputes sky light for every column. Vertical-only: full sky
    /// (15) from the top until the first block that blocks sky, then 0 all the
    /// way down. Cheap and self-contained, so it runs on the load/gen worker.</summary>
    public void ComputeSkyLight()
    {
        for (int z = 0; z < SizeZ; z++)
            for (int x = 0; x < SizeX; x++)
                RecomputeSkyColumn(x, z);
    }

    /// <summary>Recomputes one column after an edit — the only cells whose sky
    /// light a block change in that column can affect.</summary>
    public void RecomputeSkyColumn(int x, int z)
    {
        byte level = 15;
        for (int y = SizeY - 1; y >= 0; y--)
        {
            if (BlockInfo.BlocksSkyLight((BlockType)_blocks[Index(x, y, z)]))
                level = 0;
            _skyLight[Index(x, y, z)] = level;
        }
    }

    public static bool InBounds(int x, int y, int z) =>
        x >= 0 && x < SizeX && y >= 0 && y < SizeY && z >= 0 && z < SizeZ;

    // The one and only index layout: x, then z, then y.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Index(int x, int y, int z) => x + z * SizeX + y * SizeX * SizeZ;
}
