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
}
