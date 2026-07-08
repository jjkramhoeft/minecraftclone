using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Items;
using MinecraftClone.Rendering;
using MinecraftClone.World;

namespace MinecraftClone.Player;

/// <summary>
/// Everything the crosshair does to the world: targeting, timed mining with
/// tool speed/tier rules, and placing from the selected hotbar stack.
/// </summary>
public class BlockInteraction
{
    private const float Reach = 5f;
    private const float UnderTierPenalty = 1f / 3f;
    private static readonly float[] TierSpeedMultiplier = { 1f, 3f, 5f }; // hand, wooden, stone

    private readonly ChunkManager _world;
    private readonly Inventory _inventory;
    private readonly PlayerController _player;
    private readonly BlockUpdater _blockUpdater;
    private readonly ItemDrops _drops;

    private (int X, int Y, int Z) _miningPos;
    private BlockType _miningType;
    private float _progress;

    /// <summary>The block under the crosshair, if any (within reach).</summary>
    public RaycastHit? Target { get; private set; }

    /// <summary>Fired on every completed break or successful place (arm swing hook).</summary>
    public event Action ActionPerformed;

    /// <summary>Fired with the broken/placed block type (audio hook).</summary>
    public event Action<BlockType> BlockBroken;
    public event Action<BlockType> BlockPlaced;

    /// <summary>Fired with the broken block's position (block-state cleanup hook).</summary>
    public event Action<int, int, int, BlockType> BlockBrokenAt;

    /// <summary>Right-click on an interactable block (furnace, chest). Return
    /// true to absorb the click; false falls through to placement.</summary>
    public Func<int, int, int, BlockType, bool> UseBlock;

    public float BreakProgress => _progress;
    public (int X, int Y, int Z) MiningPos => _miningPos;
    public bool IsMining => _progress > 0f;

    public BlockInteraction(ChunkManager world, Inventory inventory, PlayerController player, BlockUpdater blockUpdater, ItemDrops drops)
    {
        _world = world;
        _inventory = inventory;
        _player = player;
        _blockUpdater = blockUpdater;
        _drops = drops;
    }

    public void Update(FirstPersonCamera camera, MouseState mouse, MouseState previousMouse, bool allowInput, float dt)
    {
        // Always from the player's eye, not the camera: in third person the
        // camera hangs back on a boom, and aiming from there would gift extra
        // reach and let you mine blocks behind your own head.
        Target = VoxelRaycaster.Cast(_world, _player.EyePosition, camera.Forward, Reach, out var hit)
            ? hit
            : null;

        if (!allowInput)
        {
            _progress = 0f;
            return;
        }

        bool leftHeld = mouse.LeftButton == ButtonState.Pressed;
        bool leftEdge = leftHeld && previousMouse.LeftButton == ButtonState.Released;
        bool rightEdge = mouse.RightButton == ButtonState.Pressed && previousMouse.RightButton == ButtonState.Released;

        // Buckets target the liquid itself, so they work without (and past) a
        // solid Target — handled before the ordinary mine/place flow.
        var held = _inventory.SelectedStack.Item;
        if (rightEdge && !leftHeld && held is ItemType.Bucket or ItemType.WaterBucket)
        {
            _progress = 0f;
            UseBucket(camera, held);
            return;
        }

        if (Target is not { } target)
        {
            _progress = 0f;
            return;
        }

        if (leftHeld)
        {
            UpdateMining(target, leftEdge, dt);
        }
        else
        {
            _progress = 0f;
            if (rightEdge && (target.NormalX != 0 || target.NormalY != 0 || target.NormalZ != 0))
                TryPlace(target);
        }
    }

    private void UpdateMining(RaycastHit target, bool leftEdge, float dt)
    {
        var type = _world.GetBlock(target.X, target.Y, target.Z);

        // Progress belongs to one (position, block type) pair: looking away
        // resets it, and so does the block changing under the crosshair
        // (e.g. sand falling into the mined cell).
        if ((target.X, target.Y, target.Z) != _miningPos || type != _miningType)
        {
            _miningPos = (target.X, target.Y, target.Z);
            _miningType = type;
            _progress = 0f;
        }

        float hardness = BlockInfo.GetHardness(type);
        if (hardness <= 0f)
        {
            // Instant blocks (flowers) break on the press edge, not on hold,
            // so sweeping the mouse doesn't mow everything down.
            if (leftEdge)
                BreakBlock(target, type, dropAllowed: true);
            return;
        }

        var heldItem = _inventory.SelectedStack.Item;
        bool matchingTool = ItemInfo.GetToolClass(heldItem) == BlockInfo.GetEffectiveTool(type);
        int effectiveTier = matchingTool ? ItemInfo.GetToolTier(heldItem) : 0;

        float speed = matchingTool ? TierSpeedMultiplier[Math.Min(effectiveTier, TierSpeedMultiplier.Length - 1)] : 1f;
        bool underTier = BlockInfo.GetRequiredTier(type) > effectiveTier;
        if (underTier)
            speed *= UnderTierPenalty; // still breakable, but slow and drops nothing

        _progress += dt * speed / hardness;
        if (_progress >= 1f)
        {
            BreakBlock(target, type, dropAllowed: !underTier);
            DamageHeldTool();
        }
    }

    /// <summary>One point of wear per timed break, whatever the tool hit; the
    /// tool vanishes when its durability runs out.</summary>
    private void DamageHeldTool()
    {
        int slot = _inventory.SelectedIndex;
        var stack = _inventory[slot];
        int max = ItemInfo.GetMaxDurability(stack.Item);
        if (max <= 0)
            return;
        stack.Damage++;
        _inventory[slot] = stack.Damage >= max ? ItemStack.Empty : stack;
    }

    private void BreakBlock(RaycastHit target, BlockType type, bool dropAllowed)
    {
        _world.SetBlock(target.X, target.Y, target.Z, BlockType.Air);
        _blockUpdater.NotifyBlockChanged(target.X, target.Y, target.Z);
        _progress = 0f;
        ActionPerformed?.Invoke();
        BlockBroken?.Invoke(type);
        BlockBrokenAt?.Invoke(target.X, target.Y, target.Z, type);

        if (dropAllowed)
        {
            // Drops become world entities with a pickup radius — nothing is
            // silently lost to a full inventory anymore.
            var drop = ItemInfo.GetDrop(type);
            if (drop != ItemType.None)
                _drops.Spawn(drop, target.X, target.Y, target.Z);
        }
    }

    /// <summary>Empty bucket: scoop the first water source the crosshair ray
    /// touches. Full bucket: pour a source into the hit water cell or the cell
    /// in front of the hit face. Interactable blocks still capture the click.</summary>
    private void UseBucket(FirstPersonCamera camera, ItemType held)
    {
        if (!VoxelRaycaster.Cast(_world, _player.EyePosition, camera.Forward, Reach, out var hit, includeWater: true))
            return;
        var hitType = _world.GetBlock(hit.X, hit.Y, hit.Z);

        if (BlockInfo.IsInteractable(hitType)
            && UseBlock?.Invoke(hit.X, hit.Y, hit.Z, hitType) == true)
            return;

        if (held == ItemType.Bucket)
        {
            // Only true sources are worth a bucket; flow cells just splash.
            if (hitType != BlockType.Water)
                return;
            _world.SetBlock(hit.X, hit.Y, hit.Z, BlockType.Air);
            _blockUpdater.NotifyBlockChanged(hit.X, hit.Y, hit.Z);
            _inventory[_inventory.SelectedIndex] = new ItemStack(ItemType.WaterBucket, 1);
            ActionPerformed?.Invoke();
            BlockBroken?.Invoke(BlockType.Water);
            return;
        }

        int x = hit.X, y = hit.Y, z = hit.Z;
        if (!BlockInfo.IsWater(hitType))
        {
            if (hit.NormalX == 0 && hit.NormalY == 0 && hit.NormalZ == 0)
                return;
            x += hit.NormalX;
            y += hit.NormalY;
            z += hit.NormalZ;
        }
        var cell = _world.GetBlock(x, y, z);
        if (cell == BlockType.Water || (cell != BlockType.Air && !BlockInfo.IsWater(cell)))
            return; // occupied, or already a source — don't waste the bucket
        if (!_world.IsChunkLoaded(ChunkManager.ToChunkCoord(new Vector3(x, 0, z))))
            return;
        _world.SetBlock(x, y, z, BlockType.Water);
        _blockUpdater.NotifyBlockChanged(x, y, z);
        _inventory[_inventory.SelectedIndex] = new ItemStack(ItemType.Bucket, 1);
        ActionPerformed?.Invoke();
        BlockPlaced?.Invoke(BlockType.Water);
    }

    private void TryPlace(RaycastHit target)
    {
        // Interactable blocks (furnace, chest) capture the click first.
        var targetType = _world.GetBlock(target.X, target.Y, target.Z);
        if (BlockInfo.IsInteractable(targetType)
            && UseBlock?.Invoke(target.X, target.Y, target.Z, targetType) == true)
            return;

        var stack = _inventory.SelectedStack;
        if (stack.IsEmpty || !ItemInfo.TryGetBlock(stack.Item, out var blockToPlace))
            return;

        // Placed stone sets as cobblestone (and mines back into stone) — the
        // world never has smooth stone you put there by hand.
        if (blockToPlace == BlockType.Stone)
            blockToPlace = BlockType.Cobblestone;

        int x = target.X + target.NormalX;
        int y = target.Y + target.NormalY;
        int z = target.Z + target.NormalZ;

        // Placing into water replaces it; the NotifyBlockChanged below lets the
        // surrounding water react (drain or close over the block). SetBlock
        // silently no-ops on unloaded chunks, so verify before consuming the item.
        if (BlockInfo.IsSolid(_world.GetBlock(x, y, z))
            || IntersectsPlayer(x, y, z)
            || !_world.IsChunkLoaded(ChunkManager.ToChunkCoord(new Vector3(x, 0, z))))
            return;

        // Plants need valid ground: flowers grow on grass/dirt; reeds on
        // sand/dirt/grass with water beside the supporting block (or stacked
        // on another reed).
        if (BlockInfo.IsPlant(blockToPlace))
        {
            var below = _world.GetBlock(x, y - 1, z);
            if (!BlockInfo.CanSupportPlant(blockToPlace, below))
                return;
            if (blockToPlace == BlockType.Reeds && below != BlockType.Reeds && !HasAdjacentWater(x, y - 1, z))
                return;
        }

        _world.SetBlock(x, y, z, blockToPlace);
        _blockUpdater.NotifyBlockChanged(x, y, z);
        _inventory.ConsumeFromSlot(_inventory.SelectedIndex);
        ActionPerformed?.Invoke();
        BlockPlaced?.Invoke(blockToPlace);
    }

    private bool HasAdjacentWater(int x, int y, int z) =>
        BlockInfo.IsWater(_world.GetBlock(x + 1, y, z))
        || BlockInfo.IsWater(_world.GetBlock(x - 1, y, z))
        || BlockInfo.IsWater(_world.GetBlock(x, y, z + 1))
        || BlockInfo.IsWater(_world.GetBlock(x, y, z - 1));

    private bool IntersectsPlayer(int x, int y, int z)
    {
        var p = _player.Position;
        return x + 1 > p.X - PlayerPhysics.HalfWidth && x < p.X + PlayerPhysics.HalfWidth
            && y + 1 > p.Y && y < p.Y + PlayerPhysics.Height
            && z + 1 > p.Z - PlayerPhysics.HalfWidth && z < p.Z + PlayerPhysics.HalfWidth;
    }
}
