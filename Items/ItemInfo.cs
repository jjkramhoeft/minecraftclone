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

    public static int MaxStack(ItemType item) => GetToolClass(item) == ToolClass.None ? 64 : 1;

    public static ToolClass GetToolClass(ItemType item) => item switch
    {
        ItemType.WoodenPickaxe or ItemType.StonePickaxe => ToolClass.Pickaxe,
        ItemType.WoodenAxe or ItemType.StoneAxe => ToolClass.Axe,
        ItemType.WoodenShovel or ItemType.StoneShovel => ToolClass.Shovel,
        _ => ToolClass.None,
    };

    /// <summary>0 = hand, 1 = wooden tools, 2 = stone tools.</summary>
    public static int GetToolTier(ItemType item) => item switch
    {
        ItemType.WoodenPickaxe or ItemType.WoodenAxe or ItemType.WoodenShovel => 1,
        ItemType.StonePickaxe or ItemType.StoneAxe or ItemType.StoneShovel => 2,
        _ => 0,
    };

    /// <summary>What breaking a block puts in the inventory. None = drops nothing.</summary>
    public static ItemType GetDrop(BlockType block) => block switch
    {
        BlockType.Grass => ItemType.Dirt,
        BlockType.Leaves => ItemType.None,
        BlockType.Air => ItemType.None,
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
            _ => BlockInfo.TileDirt,
        };
    }
}
