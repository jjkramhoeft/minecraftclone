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

/// <summary>Which kind of tool mines a block efficiently.</summary>
public enum ToolClass
{
    None,
    Pickaxe,
    Axe,
    Shovel,
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
    public const int TileWater = 8;
    public const int TilePlanks = 9;
    public const int TileBricks = 10;
    // UI-only item icons (not used on block faces)
    public const int TileStick = 11;
    public const int TileWoodenPickaxe = 12;
    public const int TileStonePickaxe = 13;
    public const int TileWoodenAxe = 14;
    public const int TileStoneAxe = 15;
    public const int TileWoodenShovel = 16;
    public const int TileStoneShovel = 17;
    public const int TileCrack0 = 18;
    public const int TileCrack1 = 19;
    public const int TileCrack2 = 20;
    public const int TileCrack3 = 21;

    /// <summary>Solid blocks collide, block raycasts, and hide neighboring faces.</summary>
    public static bool IsSolid(BlockType type) => type is not (BlockType.Air or BlockType.Water);

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
        BlockType.Water => TileWater,
        BlockType.Planks => TilePlanks,
        BlockType.Bricks => TileBricks,
        _ => TileDirt,
    };

    /// <summary>Seconds to break by hand (with the right tool acting as a divisor).
    /// 0 = breaks instantly on click.</summary>
    public static float GetHardness(BlockType type) => type switch
    {
        BlockType.Grass or BlockType.Dirt or BlockType.Sand => 0.75f,
        BlockType.Leaves => 0.3f,
        BlockType.Wood => 2f,
        BlockType.Planks => 1.5f,
        BlockType.Stone or BlockType.Bricks => 4f,
        _ => 1f,
    };

    public static ToolClass GetEffectiveTool(BlockType type) => type switch
    {
        BlockType.Stone or BlockType.Bricks => ToolClass.Pickaxe,
        BlockType.Wood or BlockType.Planks or BlockType.Leaves => ToolClass.Axe,
        BlockType.Grass or BlockType.Dirt or BlockType.Sand => ToolClass.Shovel,
        _ => ToolClass.None,
    };

    /// <summary>Minimum matching-tool tier for the block to drop its item.
    /// Mining below the tier still breaks the block (slowly) but yields nothing.</summary>
    public static int GetRequiredTier(BlockType type) => type switch
    {
        BlockType.Stone or BlockType.Bricks => 1,
        _ => 0,
    };

    /// <summary>Gravity blocks fall when the cell below them is not solid.</summary>
    public static bool HasGravity(BlockType type) => type == BlockType.Sand;
}
