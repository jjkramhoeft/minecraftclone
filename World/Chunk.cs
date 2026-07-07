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

    public Chunk(ChunkCoord coord) => Coord = coord;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BlockType GetBlock(int x, int y, int z) => (BlockType)_blocks[Index(x, y, z)];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(int x, int y, int z, BlockType type) => _blocks[Index(x, y, z)] = (byte)type;

    public static bool InBounds(int x, int y, int z) =>
        x >= 0 && x < SizeX && y >= 0 && y < SizeY && z >= 0 && z < SizeZ;

    // The one and only index layout: x, then z, then y.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Index(int x, int y, int z) => x + z * SizeX + y * SizeX * SizeZ;
}
