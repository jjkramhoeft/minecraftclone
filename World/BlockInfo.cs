namespace MinecraftClone.World;

/// <summary>Face order matches ChunkMesher's face tables.</summary>
public enum BlockFace
{
    Top = 0,     // +Y
    Bottom = 1,  // -Y
    North = 2,   // -Z
    South = 3,   // +Z
    East = 4,    // +X
    West = 5,    // -X
}

public static class BlockInfo
{
    // Atlas tile indices — TextureAtlas generates these tiles, the mesher and
    // hotbar look them up through GetFaceTile.
    public const int TileGrassTop = 0;
    public const int TileGrassSide = 1;
    public const int TileDirt = 2;
    public const int TileStone = 3;
    public const int TileSand = 4;
    public const int TileWoodSide = 5;
    public const int TileWoodTop = 6;
    public const int TileLeaves = 7;

    public static bool IsSolid(BlockType type) => type != BlockType.Air;

    public static int GetFaceTile(BlockType type, BlockFace face) => type switch
    {
        BlockType.Grass => face switch
        {
            BlockFace.Top => TileGrassTop,
            BlockFace.Bottom => TileDirt,
            _ => TileGrassSide,
        },
        BlockType.Dirt => TileDirt,
        BlockType.Stone => TileStone,
        BlockType.Sand => TileSand,
        BlockType.Wood => face is BlockFace.Top or BlockFace.Bottom ? TileWoodTop : TileWoodSide,
        BlockType.Leaves => TileLeaves,
        _ => TileDirt,
    };
}
