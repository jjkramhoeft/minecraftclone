namespace MinecraftClone.Items;

/// <summary>
/// Everything a player can hold. Values 0–31 mirror BlockType byte values 1:1
/// (0 = empty, like Air), so block↔item conversion is a plain cast. Non-block
/// items start at 32. Values are persisted in world.json — append only, never
/// renumber.
/// </summary>
public enum ItemType : ushort
{
    None = 0,
    Grass = 1,
    Dirt = 2,
    Stone = 3,
    Sand = 4,
    Wood = 5,
    Leaves = 6,
    // 7 = Water, never an item
    Planks = 8,
    Bricks = 9,
    FlowerRed = 10,
    FlowerYellow = 11,
    FlowerPoppy = 12,
    Reeds = 13,
    // 14-21 = water variants, never items
    CoalOre = 22,
    IronOre = 23,
    Torch = 24,
    Furnace = 25,
    // 26 = FurnaceLit, never an item
    Glass = 27,
    Chest = 28,

    Stick = 32,
    WoodenPickaxe = 33,
    StonePickaxe = 34,
    WoodenAxe = 35,
    StoneAxe = 36,
    WoodenShovel = 37,
    StoneShovel = 38,
    Coal = 39,
    IronIngot = 40,
    IronPickaxe = 41,
    IronAxe = 42,
    IronShovel = 43,
}
