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
    public const int TileFlowerRed = 22;
    public const int TileFlowerYellow = 23;
    public const int TileFlowerPoppy = 24;
    // Player character skin (UI/model only, never on block faces)
    public const int TileSkin = 25;
    public const int TileShirt = 26;
    public const int TilePants = 27;
    public const int TileFace = 28;
    public const int TileReeds = 29;
    // Sky sprites (never on block faces)
    public const int TileSun = 30;
    public const int TileMoon = 31;
    public const int TileCoalOre = 32;
    public const int TileIronOre = 33;
    // UI-only item icons
    public const int TileIronPickaxe = 34;
    public const int TileIronAxe = 35;
    public const int TileIronShovel = 36;
    public const int TileCoal = 37;
    public const int TileIronIngot = 38;
    public const int TileTorch = 39;
    // Mob skins (never on block faces)
    public const int TilePig = 40;
    public const int TileChicken = 41;
    public const int TileFurnaceSide = 42;
    public const int TileFurnaceFront = 43;
    public const int TileFurnaceFrontLit = 44;
    public const int TileGlass = 45;
    public const int TileChestSide = 46;
    public const int TileChestFront = 47;
    public const int TileBucket = 48;
    public const int TileWaterBucket = 49;
    public const int TileCraftingTop = 50;
    public const int TileCraftingSide = 51;
    public const int TileBirchBark = 52;
    public const int TileBirchTop = 53;
    public const int TileBirchLeaves = 54;
    public const int TilePineBark = 55;
    public const int TilePineTop = 56;
    public const int TilePineLeaves = 57;

    /// <summary>Solid blocks collide and cast ambient occlusion. Plants are
    /// deliberately NOT solid — you walk through them.</summary>
    public static bool IsSolid(BlockType type) =>
        type != BlockType.Air && !IsWater(type) && !IsPlant(type);

    /// <summary>Opaque blocks hide their neighbors' touching faces. Glass is
    /// solid but see-through, so it never culls anything.</summary>
    public static bool IsOpaque(BlockType type) =>
        IsSolid(type) && type != BlockType.Glass;

    /// <summary>Blocks that respond to right-click instead of being built against.</summary>
    public static bool IsInteractable(BlockType type) =>
        type is BlockType.Furnace or BlockType.FurnaceLit or BlockType.Chest or BlockType.CraftingTable;

    /// <summary>Source water and every flowing/falling variant.</summary>
    public static bool IsWater(BlockType type) =>
        type is BlockType.Water or (>= BlockType.WaterFlow1 and <= BlockType.WaterFall);

    /// <summary>Source and falling water are level 8; WaterFlowN is N; non-water is 0.</summary>
    public static int GetWaterLevel(BlockType type) => type switch
    {
        BlockType.Water or BlockType.WaterFall => 8,
        >= BlockType.WaterFlow1 and <= BlockType.WaterFlow7 => type - BlockType.WaterFlow1 + 1,
        _ => 0,
    };

    /// <summary>The flowing-water block for a level in 1..7.</summary>
    public static BlockType FlowBlockForLevel(int level) =>
        (BlockType)((int)BlockType.WaterFlow1 + level - 1);

    public static bool IsFlower(BlockType type) =>
        type is >= BlockType.FlowerRed and <= BlockType.FlowerPoppy;

    /// <summary>Plants render as crossed quads in the cutout pass, break
    /// instantly, and need a supporting block below. Torches behave the same
    /// way except they stand on any solid block.</summary>
    public static bool IsPlant(BlockType type) =>
        IsFlower(type) || type is BlockType.Reeds or BlockType.Torch;

    /// <summary>What a plant may stand on. Reeds also stack on themselves.</summary>
    public static bool CanSupportPlant(BlockType plant, BlockType below) => plant switch
    {
        BlockType.Reeds => below is BlockType.Sand or BlockType.Dirt or BlockType.Grass or BlockType.Reeds,
        BlockType.Torch => IsSolid(below),
        _ => below is BlockType.Grass or BlockType.Dirt,
    };

    /// <summary>Block-light level (0-15) this block radiates.</summary>
    public static byte GetLightEmission(BlockType type) => type switch
    {
        BlockType.Torch => 14,
        BlockType.FurnaceLit => 13,
        _ => 0,
    };

    /// <summary>What the crosshair raycast can hit: solid blocks and plants,
    /// but never water or air.</summary>
    public static bool IsTargetable(BlockType type) => IsSolid(type) || IsPlant(type);

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
        BlockType.BirchLog => face is BlockFace.Top or BlockFace.Bottom ? TileBirchTop : TileBirchBark,
        BlockType.PineLog => face is BlockFace.Top or BlockFace.Bottom ? TilePineTop : TilePineBark,
        BlockType.Leaves => TileLeaves,
        BlockType.BirchLeaves => TileBirchLeaves,
        BlockType.PineLeaves => TilePineLeaves,
        BlockType.Water => TileWater,
        BlockType.Planks => TilePlanks,
        BlockType.Bricks => TileBricks,
        BlockType.FlowerRed => TileFlowerRed,
        BlockType.FlowerYellow => TileFlowerYellow,
        BlockType.FlowerPoppy => TileFlowerPoppy,
        BlockType.Reeds => TileReeds,
        BlockType.CoalOre => TileCoalOre,
        BlockType.IronOre => TileIronOre,
        BlockType.Torch => TileTorch,
        // No block orientation, so the mouth shows on all four sides.
        BlockType.Furnace or BlockType.FurnaceLit => face switch
        {
            BlockFace.Top or BlockFace.Bottom => TileFurnaceSide,
            _ => type == BlockType.FurnaceLit ? TileFurnaceFrontLit : TileFurnaceFront,
        },
        BlockType.Glass => TileGlass,
        BlockType.Chest => face switch
        {
            BlockFace.Top or BlockFace.Bottom => TileChestSide,
            BlockFace.South => TileChestFront, // no orientation; latch faces +Z
            _ => TileChestSide,
        },
        BlockType.CraftingTable => face switch
        {
            BlockFace.Top => TileCraftingTop,
            BlockFace.Bottom => TilePlanks,
            _ => TileCraftingSide,
        },
        _ when IsWater(type) => TileWater,
        _ => TileDirt,
    };

    /// <summary>Seconds to break by hand (with the right tool acting as a divisor).
    /// 0 = breaks instantly on click.</summary>
    public static float GetHardness(BlockType type) => type switch
    {
        BlockType.Grass or BlockType.Dirt or BlockType.Sand => 0.75f,
        BlockType.Leaves or BlockType.BirchLeaves or BlockType.PineLeaves => 0.3f,
        BlockType.Wood or BlockType.BirchLog or BlockType.PineLog => 2f,
        BlockType.Planks or BlockType.Chest or BlockType.CraftingTable => 1.5f,
        BlockType.Stone or BlockType.Bricks or BlockType.Furnace or BlockType.FurnaceLit => 4f,
        BlockType.CoalOre or BlockType.IronOre => 5f,
        BlockType.Glass => 0.4f,
        _ when IsPlant(type) => 0f, // instant break
        _ => 1f,
    };

    public static ToolClass GetEffectiveTool(BlockType type) => type switch
    {
        BlockType.Stone or BlockType.Bricks or BlockType.CoalOre or BlockType.IronOre
            or BlockType.Furnace or BlockType.FurnaceLit => ToolClass.Pickaxe,
        BlockType.Wood or BlockType.Planks or BlockType.Leaves or BlockType.Chest
            or BlockType.CraftingTable or BlockType.BirchLog or BlockType.PineLog
            or BlockType.BirchLeaves or BlockType.PineLeaves => ToolClass.Axe,
        BlockType.Grass or BlockType.Dirt or BlockType.Sand => ToolClass.Shovel,
        _ => ToolClass.None,
    };

    /// <summary>Minimum matching-tool tier for the block to drop its item.
    /// Mining below the tier still breaks the block (slowly) but yields nothing.</summary>
    public static int GetRequiredTier(BlockType type) => type switch
    {
        BlockType.Stone or BlockType.Bricks or BlockType.CoalOre
            or BlockType.Furnace or BlockType.FurnaceLit => 1,
        BlockType.IronOre => 2,
        _ => 0,
    };

    /// <summary>Gravity blocks fall when the cell below them is not solid.</summary>
    public static bool HasGravity(BlockType type) => type == BlockType.Sand;
}
