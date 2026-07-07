using System.Collections.Generic;

namespace MinecraftClone.World;

/// <summary>
/// Cell-by-cell falling for gravity blocks (sand). Player edits notify the
/// updater, which re-checks the disturbed cells and their neighbors on a fixed
/// tick; a block with a non-solid cell below moves down one cell per tick
/// (destroying flowers / displacing water — there is no flow simulation) and
/// re-disturbs the cells above and below it, so columns cascade.
///
/// Deliberately not hooked into ChunkManager.SetBlock: worldgen and chunk
/// loading must never cause update storms. Every move goes through SetBlock,
/// so saving (IsModified) and the stale-mesh guard (Version) work unchanged.
/// The pending set is not persisted — sand left floating in an unloaded chunk
/// stays floating until something disturbs it again.
/// </summary>
public class BlockUpdater
{
    private const float TickInterval = 0.1f;
    private const int MaxUpdatesPerTick = 256; // bounds the cost of huge cascades

    private readonly HashSet<(int X, int Y, int Z)> _pending = new();
    private readonly List<(int X, int Y, int Z)> _processing = new();
    private float _tickTimer;

    /// <summary>Call after any block change; queues the cell and its six neighbors.</summary>
    public void NotifyBlockChanged(int x, int y, int z)
    {
        _pending.Add((x, y, z));
        _pending.Add((x + 1, y, z));
        _pending.Add((x - 1, y, z));
        _pending.Add((x, y + 1, z));
        _pending.Add((x, y - 1, z));
        _pending.Add((x, y, z + 1));
        _pending.Add((x, y, z - 1));
    }

    public void Update(ChunkManager world, float dt)
    {
        _tickTimer += dt;
        if (_tickTimer < TickInterval || _pending.Count == 0)
            return;
        _tickTimer = 0f;

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
            if (!BlockInfo.HasGravity(block) || BlockInfo.IsSolid(world.GetBlock(x, y - 1, z)))
                continue;

            world.SetBlock(x, y, z, BlockType.Air);
            world.SetBlock(x, y - 1, z, block);
            _pending.Add((x, y - 1, z)); // keep falling next tick
            _pending.Add((x, y + 1, z)); // the column above lost its support
        }
    }
}
