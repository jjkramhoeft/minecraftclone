namespace MinecraftClone.World;

public enum BlockType : byte
{
    Air = 0,
    Grass,
    Dirt,
    Stone,
    Sand,
    Wood,
    Leaves,
    Water,
    Planks,
    Bricks,
    FlowerRed,
    FlowerYellow,
    FlowerPoppy,
    Reeds,
    // Flowing water, level 1 (thinnest) .. 7 (next to source). Contiguous and
    // ascending — BlockInfo.GetWaterLevel/FlowBlockForLevel rely on the order.
    WaterFlow1 = 14,
    WaterFlow2,
    WaterFlow3,
    WaterFlow4,
    WaterFlow5,
    WaterFlow6,
    WaterFlow7 = 20,
    // Water that arrived by falling; full height, sustained only by water above.
    WaterFall = 21,
    CoalOre = 22,
    IronOre = 23,
    Torch = 24,
    Furnace = 25,
    FurnaceLit = 26, // furnace mid-smelt: emits light, never an item
    Glass = 27,
    Chest = 28,
    CraftingTable = 29,
}
