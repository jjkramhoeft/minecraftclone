using System;
using System.Collections.Generic;

namespace MinecraftClone.World;

/// <summary>
/// Smoothly animated falling blocks. When BlockUpdater finds an unsupported
/// gravity block it leaves the grid (SetBlock Air) and becomes an entry here:
/// a continuous Y position accelerating downward each frame. On landing the
/// block re-enters the grid via SetBlock (crushing plants / displacing water)
/// and re-disturbs its neighborhood so columns keep cascading.
/// </summary>
public class FallingBlocks
{
    public class Entry
    {
        public BlockType Type;
        public int X, Z;
        public float Y;
        public float Velocity;
    }

    private const float Gravity = -16f;         // gentler than the player's -25 — reads better
    private const float TerminalVelocity = -12f;

    private readonly List<Entry> _entries = new();

    public IReadOnlyList<Entry> Entries => _entries;

    public void Spawn(BlockType type, int x, int y, int z) =>
        _entries.Add(new Entry { Type = type, X = x, Z = z, Y = y, Velocity = 0f });

    public void Update(ChunkManager world, BlockUpdater updater, float dt)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];

            if (!world.IsChunkLoaded(new ChunkCoord(entry.X >> 4, entry.Z >> 4)))
            {
                _entries.RemoveAt(i); // fell out of the loaded world — gone
                continue;
            }

            entry.Velocity = Math.Max(entry.Velocity + Gravity * dt, TerminalVelocity);
            float targetY = entry.Y + entry.Velocity * dt;

            // Check every cell crossed this frame so fast falls can't tunnel.
            bool settled = false;
            for (int y = (int)MathF.Floor(entry.Y); y >= (int)MathF.Floor(targetY); y--)
            {
                if (y <= 0 || BlockInfo.IsSolid(world.GetBlock(entry.X, y - 1, entry.Z)))
                {
                    Settle(world, updater, entry, Math.Max(y, 0));
                    _entries.RemoveAt(i);
                    settled = true;
                    break;
                }
            }

            if (!settled)
                entry.Y = targetY;
        }
    }

    /// <summary>Instantly lands everything — called before the game exits so a
    /// mid-air block is never lost.</summary>
    public void SettleAll(ChunkManager world, BlockUpdater updater)
    {
        foreach (var entry in _entries)
        {
            int y = (int)MathF.Floor(entry.Y);
            while (y > 0 && !BlockInfo.IsSolid(world.GetBlock(entry.X, y - 1, entry.Z)))
                y--;
            Settle(world, updater, entry, Math.Max(y, 0));
        }
        _entries.Clear();
    }

    public void Clear() => _entries.Clear();

    private static void Settle(ChunkManager world, BlockUpdater updater, Entry entry, int y)
    {
        // The rest cell may have been filled while falling (e.g. the player
        // built there) — rise to the first free cell.
        while (y < Chunk.SizeY - 1 && BlockInfo.IsSolid(world.GetBlock(entry.X, y, entry.Z)))
            y++;
        world.SetBlock(entry.X, y, entry.Z, entry.Type);
        updater.NotifyBlockChanged(entry.X, y, entry.Z);
    }
}
