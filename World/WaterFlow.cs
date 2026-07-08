using System;

namespace MinecraftClone.World;

/// <summary>
/// Minecraft-classic water flow as a self-verifying cellular automaton over
/// BlockUpdater's disturbed-cell set. Each processed water cell recomputes what
/// it should be from its neighbors (source water above => falling, else one
/// level below its highest side neighbor) and corrects itself, then spreads:
/// down first, sideways only when it can't fall. Sources (BlockType.Water) are
/// never destroyed by the simulation, and created only by the infinite-water
/// rule (a flowing cell flanked by two sources over solid ground).
///
/// Stability: a cell already in its expected state with nothing to spread into
/// performs zero writes and zero notifications, so a disturbance next to the
/// ocean ripples one cell in and dies instead of re-simulating the ocean.
/// Draining converges because an orphaned flow region's maximum level strictly
/// decreases every water tick.
/// </summary>
public static class WaterFlow
{
    public static void Simulate(ChunkManager world, BlockUpdater updater,
        int x, int y, int z, BlockType block)
    {
        var above = world.GetBlock(x, y + 1, z);

        if (block != BlockType.Water)
        {
            // Next to an unloaded chunk, GetBlock reports Air and the sustain
            // check would drain lakes at the streaming horizon — freeze the
            // cell until re-disturbed, same policy as floating sand.
            if (!HorizontalNeighborsLoaded(world, x, z))
                return;

            var expected = ExpectedState(world, x, y, z, above);
            if (expected != block)
            {
                if (!world.SetBlockDeferred(x, y, z, expected))
                    return;
                updater.NotifyBlockChanged(x, y, z);
                if (expected == BlockType.Air)
                    return;
                block = expected;
            }
        }

        // Down first: water that can fall does not spread sideways this tick.
        var below = world.GetBlock(x, y - 1, z);
        if (below == BlockType.Air)
        {
            if (y - 1 >= 1 && world.SetBlockDeferred(x, y - 1, z, BlockType.WaterFall))
                updater.NotifyBlockChanged(x, y - 1, z);
            return;
        }
        // Flowing water over water merges into it — no sideways spray from a
        // waterfall mid-column or from a channel mouth above the ocean. Sources
        // DO spread over water, so a hole dug in an underwater wall fills in.
        if (block != BlockType.Water && BlockInfo.IsWater(below))
            return;

        int spreadLevel = BlockInfo.GetWaterLevel(block) - 1;
        if (spreadLevel < 1)
            return;
        SpreadInto(world, updater, x + 1, y, z, spreadLevel);
        SpreadInto(world, updater, x - 1, y, z, spreadLevel);
        SpreadInto(world, updater, x, y, z + 1, spreadLevel);
        SpreadInto(world, updater, x, y, z - 1, spreadLevel);
    }

    /// <summary>What a flowing cell should be, given its surroundings.</summary>
    private static BlockType ExpectedState(ChunkManager world, int x, int y, int z, BlockType above)
    {
        if (BlockInfo.IsWater(above))
            return BlockType.WaterFall;

        // Infinite-water rule: two adjacent sources over solid ground breed a
        // third, so dipping a bucket into a pool doesn't leave a hole. Ocean
        // cells are already sources, so this never churns standing water.
        int sources =
            (world.GetBlock(x + 1, y, z) == BlockType.Water ? 1 : 0)
            + (world.GetBlock(x - 1, y, z) == BlockType.Water ? 1 : 0)
            + (world.GetBlock(x, y, z + 1) == BlockType.Water ? 1 : 0)
            + (world.GetBlock(x, y, z - 1) == BlockType.Water ? 1 : 0);
        if (sources >= 2 && BlockInfo.IsSolid(world.GetBlock(x, y - 1, z)))
            return BlockType.Water;

        int maxSide = Math.Max(
            Math.Max(BlockInfo.GetWaterLevel(world.GetBlock(x + 1, y, z)),
                     BlockInfo.GetWaterLevel(world.GetBlock(x - 1, y, z))),
            Math.Max(BlockInfo.GetWaterLevel(world.GetBlock(x, y, z + 1)),
                     BlockInfo.GetWaterLevel(world.GetBlock(x, y, z - 1))));
        return maxSide >= 2 ? BlockInfo.FlowBlockForLevel(maxSide - 1) : BlockType.Air;
    }

    private static void SpreadInto(ChunkManager world, BlockUpdater updater, int x, int y, int z, int level)
    {
        // Only empty cells fill; plants block water. SetBlockDeferred refuses
        // unloaded chunks, so spread stops cleanly at the streaming horizon.
        if (world.GetBlock(x, y, z) != BlockType.Air)
            return;
        if (world.SetBlockDeferred(x, y, z, BlockInfo.FlowBlockForLevel(level)))
            updater.NotifyBlockChanged(x, y, z);
    }

    private static bool HorizontalNeighborsLoaded(ChunkManager world, int x, int z)
    {
        int lx = x & 15, lz = z & 15;
        if (lx == 0 && !world.IsChunkLoaded(new ChunkCoord((x - 1) >> 4, z >> 4))) return false;
        if (lx == 15 && !world.IsChunkLoaded(new ChunkCoord((x + 1) >> 4, z >> 4))) return false;
        if (lz == 0 && !world.IsChunkLoaded(new ChunkCoord(x >> 4, (z - 1) >> 4))) return false;
        if (lz == 15 && !world.IsChunkLoaded(new ChunkCoord(x >> 4, (z + 1) >> 4))) return false;
        return true;
    }
}
