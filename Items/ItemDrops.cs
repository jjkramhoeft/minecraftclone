using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MinecraftClone.World;

namespace MinecraftClone.Items;

/// <summary>
/// Items lying in the world as small bobbing cubes. Broken blocks spawn one
/// with a little upward pop; it falls to rest above the ground and is vacuumed
/// into the inventory when the player comes near (unless the inventory is
/// full, in which case it just stays). Ambient like falling blocks — never
/// saved; drops despawn after a few minutes.
/// </summary>
public class ItemDrops
{
    public class Drop
    {
        public ItemType Item;
        public Vector3 Position; // center of the cube
        public float VelocityY;
        public float Age;
    }

    private const float Gravity = -18f;
    private const float RestHeight = 0.25f; // cube center above the ground
    private const float PickupRadius = 1.4f;
    private const float PickupDelay = 0.4f;  // seconds before it can be collected
    private const float DespawnSeconds = 300f;

    private readonly List<Drop> _drops = new();

    public IReadOnlyList<Drop> All => _drops;

    /// <summary>Fired when a drop lands in the inventory (sound hook).</summary>
    public event Action PickedUp;

    public void Clear() => _drops.Clear();

    public void Spawn(ItemType item, int blockX, int blockY, int blockZ)
    {
        if (item == ItemType.None)
            return;
        _drops.Add(new Drop
        {
            Item = item,
            Position = new Vector3(blockX + 0.5f, blockY + 0.5f, blockZ + 0.5f),
            VelocityY = 3f, // little pop upward
        });
    }

    public void Update(ChunkManager world, Inventory inventory, Vector3 playerFeet, float dt)
    {
        var playerCenter = playerFeet + new Vector3(0f, 0.9f, 0f);

        for (int i = _drops.Count - 1; i >= 0; i--)
        {
            var drop = _drops[i];
            drop.Age += dt;
            if (drop.Age > DespawnSeconds
                || !world.IsChunkLoaded(ChunkManager.ToChunkCoord(drop.Position)))
            {
                _drops.RemoveAt(i);
                continue;
            }

            // Fall until resting just above the first solid block below.
            drop.VelocityY = Math.Max(drop.VelocityY + Gravity * dt, -20f);
            float newY = drop.Position.Y + drop.VelocityY * dt;
            int cellBelow = (int)MathF.Floor(newY - RestHeight);
            if (drop.VelocityY < 0f && BlockInfo.IsSolid(world.GetBlock(
                (int)MathF.Floor(drop.Position.X), cellBelow, (int)MathF.Floor(drop.Position.Z))))
            {
                newY = cellBelow + 1 + RestHeight;
                drop.VelocityY = 0f;
            }
            drop.Position.Y = newY;

            if (drop.Age >= PickupDelay
                && Vector3.DistanceSquared(drop.Position, playerCenter) <= PickupRadius * PickupRadius
                && inventory.TryAdd(drop.Item))
            {
                _drops.RemoveAt(i);
                PickedUp?.Invoke();
            }
        }
    }
}
