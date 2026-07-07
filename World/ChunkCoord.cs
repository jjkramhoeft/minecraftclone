namespace MinecraftClone.World;

/// <summary>Position of a column chunk in the 2D chunk grid (world XZ divided by chunk size).</summary>
public readonly record struct ChunkCoord(int X, int Z);
