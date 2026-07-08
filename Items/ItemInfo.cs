using MinecraftClone.World;

namespace MinecraftClone.Items;

/// <summary>Static per-item data, in the BlockInfo style. Items reference the
/// World layer (never the other way around).</summary>
public static class ItemInfo
{
    /// <summary>Block items are ids 1–31 excluding water (any variant); the cast
    /// is safe because ItemType mirrors BlockType in that range.</summary>
    public static bool TryGetBlock(ItemType item, out BlockType block)
    {
        block = default;
        ushort id = (ushort)item;
        if (id == 0 || id >= 32 || BlockInfo.IsWater((BlockType)(byte)id))
            return false;
        block = (BlockType)(byte)id;
        return true;
    }

    public static ItemType FromBlock(BlockType block) => (ItemType)(byte)block;

    public static int MaxStack(ItemType item) =>
        item is ItemType.Bucket or ItemType.WaterBucket || GetToolClass(item) != ToolClass.None ? 1 : 64;

    /// <summary>Items the first-person view shows held as a flat sprite in the
    /// hand: tools and the bucket. Blocks and loose items stay hand-only.</summary>
    public static bool IsHeldInHand(ItemType item) =>
        GetToolClass(item) != ToolClass.None || item is ItemType.Bucket or ItemType.WaterBucket;

    public static ToolClass GetToolClass(ItemType item) => item switch
    {
        ItemType.WoodenPickaxe or ItemType.StonePickaxe or ItemType.IronPickaxe => ToolClass.Pickaxe,
        ItemType.WoodenAxe or ItemType.StoneAxe or ItemType.IronAxe => ToolClass.Axe,
        ItemType.WoodenShovel or ItemType.StoneShovel or ItemType.IronShovel => ToolClass.Shovel,
        _ => ToolClass.None,
    };

    /// <summary>Block breaks a tool survives; 0 = not damageable.</summary>
    public static int GetMaxDurability(ItemType item) => GetToolTier(item) switch
    {
        1 => 60,
        2 => 132,
        3 => 251,
        _ => 0,
    };

    /// <summary>0 = hand, 1 = wooden tools, 2 = stone tools, 3 = iron tools.</summary>
    public static int GetToolTier(ItemType item) => item switch
    {
        ItemType.WoodenPickaxe or ItemType.WoodenAxe or ItemType.WoodenShovel => 1,
        ItemType.StonePickaxe or ItemType.StoneAxe or ItemType.StoneShovel => 2,
        ItemType.IronPickaxe or ItemType.IronAxe or ItemType.IronShovel => 3,
        _ => 0,
    };

    /// <summary>What breaking a block puts in the inventory. None = drops nothing.</summary>
    public static ItemType GetDrop(BlockType block) => block switch
    {
        BlockType.Grass => ItemType.Dirt,
        BlockType.Leaves or BlockType.BirchLeaves or BlockType.PineLeaves => ItemType.None,
        // Birch/pine logs aren't items themselves; chopping them yields plain wood.
        BlockType.BirchLog or BlockType.PineLog => ItemType.Wood,
        BlockType.Fern => ItemType.None,
        // Cobblestone (placed stone) mines back into a plain stone item.
        BlockType.Cobblestone => ItemType.Stone,
        BlockType.Air => ItemType.None,
        BlockType.CoalOre => ItemType.Coal,
        BlockType.FurnaceLit => ItemType.Furnace,
        _ when BlockInfo.IsWater(block) => ItemType.None,
        _ => FromBlock(block),
    };

    /// <summary>Atlas tile used to draw this item in UI slots.</summary>
    public static int GetIconTile(ItemType item)
    {
        if (TryGetBlock(item, out var block))
            return BlockInfo.GetFaceTile(block, BlockFace.South);
        return item switch
        {
            ItemType.Stick => BlockInfo.TileStick,
            ItemType.WoodenPickaxe => BlockInfo.TileWoodenPickaxe,
            ItemType.StonePickaxe => BlockInfo.TileStonePickaxe,
            ItemType.WoodenAxe => BlockInfo.TileWoodenAxe,
            ItemType.StoneAxe => BlockInfo.TileStoneAxe,
            ItemType.WoodenShovel => BlockInfo.TileWoodenShovel,
            ItemType.StoneShovel => BlockInfo.TileStoneShovel,
            ItemType.IronPickaxe => BlockInfo.TileIronPickaxe,
            ItemType.IronAxe => BlockInfo.TileIronAxe,
            ItemType.IronShovel => BlockInfo.TileIronShovel,
            ItemType.Coal => BlockInfo.TileCoal,
            ItemType.IronIngot => BlockInfo.TileIronIngot,
            ItemType.Bucket => BlockInfo.TileBucket,
            ItemType.WaterBucket => BlockInfo.TileWaterBucket,
            _ => BlockInfo.TileDirt,
        };
    }
}
