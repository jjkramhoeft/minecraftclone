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
    // Tree-species variants — worldgen only, never held as items (birch/pine
    // logs drop plain Wood, their leaves drop nothing), so they sit above the
    // item-mirrored id range (see ItemType) and need no ItemType entry. Any
    // block with an id >= 30 must define an explicit ItemInfo.GetDrop, or the
    // default FromBlock cast produces a bogus item.
    BirchLog = 30,
    BirchLeaves = 31,
    PineLog = 32,
    PineLeaves = 33,
    // Ground cover near pines. A plant (cross-quad, breaks instantly), not an
    // item — it drops nothing, so like the tree variants it needs no ItemType.
    Fern = 34,
}
