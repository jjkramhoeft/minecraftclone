using Microsoft.Xna.Framework;

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
    public static bool IsSolid(BlockType type) => type != BlockType.Air;

    // Phase 2: flat per-face colors. Phase 6 replaces these with texture atlas tiles.
    public static Color GetFaceColor(BlockType type, BlockFace face) => type switch
    {
        BlockType.Grass => face switch
        {
            BlockFace.Top => new Color(96, 176, 64),
            BlockFace.Bottom => new Color(134, 96, 67),
            _ => new Color(115, 134, 62),
        },
        BlockType.Dirt => new Color(134, 96, 67),
        BlockType.Stone => new Color(125, 125, 125),
        BlockType.Sand => new Color(219, 207, 163),
        BlockType.Wood => new Color(102, 81, 50),
        BlockType.Leaves => new Color(58, 138, 42),
        _ => Color.Magenta,
    };
}
