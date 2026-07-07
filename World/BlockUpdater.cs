using System.Collections.Generic;

namespace MinecraftClone.World;

/// <summary>
/// Block updates on a fixed tick over a pending set of disturbed cells: an
/// unsupported gravity block leaves the grid and becomes a smoothly animated
/// FallingBlocks entry; an unsupported plant pops. The cell above a change is
/// re-disturbed, so columns cascade. Water keeps its own pending set and runs
/// every 2nd tick with a separate budget (see WaterFlow).
///
/// Deliberately not hooked into ChunkManager.SetBlock: worldgen and chunk
/// loading must never cause update storms. Every change goes through SetBlock,
/// so saving (IsModified) and the stale-mesh guard (Version) work unchanged.
/// The pending set is not persisted — sand left floating in an unloaded chunk
/// stays floating until something disturbs it again.
/// </summary>
public class BlockUpdater
{
    private const float TickInterval = 0.1f;
    private const int MaxUpdatesPerTick = 256; // bounds the cost of huge cascades
    private const int WaterTickDivider = 2;    // water flows every 2nd tick (0.2 s)
    private const int MaxWaterUpdatesPerTick = 256; // separate budget: waterfalls can't starve sand

    private readonly HashSet<(int X, int Y, int Z)> _pending = new();
    private readonly List<(int X, int Y, int Z)> _processing = new();
    private readonly HashSet<(int X, int Y, int Z)> _pendingWater = new();
    private readonly List<(int X, int Y, int Z)> _processingWater = new();
    private float _tickTimer;
    private int _tickCount;

    /// <summary>Call after any block change; queues the cell and its six neighbors.</summary>
    public void NotifyBlockChanged(int x, int y, int z)
    {
        Enqueue(_pending, x, y, z);
        Enqueue(_pendingWater, x, y, z);
    }

    private static void Enqueue(HashSet<(int X, int Y, int Z)> set, int x, int y, int z)
    {
        set.Add((x, y, z));
        set.Add((x + 1, y, z));
        set.Add((x - 1, y, z));
        set.Add((x, y + 1, z));
        set.Add((x, y - 1, z));
        set.Add((x, y, z + 1));
        set.Add((x, y, z - 1));
    }

    public void Update(ChunkManager world, FallingBlocks fallingBlocks, float dt)
    {
        _tickTimer += dt;
        if (_tickTimer < TickInterval || (_pending.Count == 0 && _pendingWater.Count == 0))
            return;
        _tickTimer = 0f;
        _tickCount++;

        UpdateSolids(world, fallingBlocks);
        if (_tickCount % WaterTickDivider == 0)
            UpdateWater(world);
    }

    private void UpdateSolids(ChunkManager world, FallingBlocks fallingBlocks)
    {
        _processing.Clear();
        foreach (var pos in _pending)
        {
            _processing.Add(pos);
            if (_processing.Count >= MaxUpdatesPerTick)
                break;
        }
        foreach (var pos in _processing)
            _pending.Remove(pos);

        foreach (var (x, y, z) in _processing)
        {
            if (y <= 0 || y >= Chunk.SizeY)
                continue;
            if (!world.IsChunkLoaded(new ChunkCoord(x >> 4, z >> 4)))
                continue; // dropped silently; re-disturbed whenever someone edits nearby again

            var block = world.GetBlock(x, y, z);

            // Plants pop (without a drop) when their supporting block is gone,
            // so digging out the ground never leaves them floating. Reed
            // stacks cascade via the re-disturbed cell above.
            if (BlockInfo.IsPlant(block))
            {
                if (!BlockInfo.CanSupportPlant(block, world.GetBlock(x, y - 1, z)))
                {
                    world.SetBlock(x, y, z, BlockType.Air);
                    _pending.Add((x, y + 1, z));
                }
                continue;
            }

            if (!BlockInfo.HasGravity(block) || BlockInfo.IsSolid(world.GetBlock(x, y - 1, z)))
                continue;

            // Leave the grid and fall as a smoothly animated entry; it places
            // itself back into the world when it lands.
            world.SetBlock(x, y, z, BlockType.Air);
            fallingBlocks.Spawn(block, x, y, z);
            _pending.Add((x, y + 1, z)); // the column above lost its support
        }
    }

    private void UpdateWater(ChunkManager world)
    {
        _processingWater.Clear();
        foreach (var pos in _pendingWater)
        {
            _processingWater.Add(pos);
            if (_processingWater.Count >= MaxWaterUpdatesPerTick)
                break;
        }
        foreach (var pos in _processingWater)
            _pendingWater.Remove(pos);

        foreach (var (x, y, z) in _processingWater)
        {
            if (y <= 0 || y >= Chunk.SizeY)
                continue;
            if (!world.IsChunkLoaded(new ChunkCoord(x >> 4, z >> 4)))
                continue; // dropped silently; re-disturbed whenever someone edits nearby again

            var block = world.GetBlock(x, y, z);
            if (BlockInfo.IsWater(block))
                WaterFlow.Simulate(world, this, x, y, z, block);
        }
    }
}
