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

    private (int X, int Y, int Z) _miningPos;
    private BlockType _miningType;
    private float _progress;

    /// <summary>The block under the crosshair, if any (within reach).</summary>
    public RaycastHit? Target { get; private set; }

    /// <summary>Fired on every completed break or successful place (arm swing hook).</summary>
    public event Action ActionPerformed;

    public float BreakProgress => _progress;
    public (int X, int Y, int Z) MiningPos => _miningPos;
    public bool IsMining => _progress > 0f;

    public BlockInteraction(ChunkManager world, Inventory inventory, PlayerController player, BlockUpdater blockUpdater)
    {
        _world = world;
        _inventory = inventory;
        _player = player;
        _blockUpdater = blockUpdater;
    }

    public void Update(FirstPersonCamera camera, MouseState mouse, MouseState previousMouse, bool allowInput, float dt)
    {
        // Always from the player's eye, not the camera: in third person the
        // camera hangs back on a boom, and aiming from there would gift extra
        // reach and let you mine blocks behind your own head.
        Target = VoxelRaycaster.Cast(_world, _player.EyePosition, camera.Forward, Reach, out var hit)
            ? hit
            : null;

        if (!allowInput || Target is not { } target)
        {
            _progress = 0f;
            return;
        }

        bool leftHeld = mouse.LeftButton == ButtonState.Pressed;
        bool leftEdge = leftHeld && previousMouse.LeftButton == ButtonState.Released;
        bool rightEdge = mouse.RightButton == ButtonState.Pressed && previousMouse.RightButton == ButtonState.Released;

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
            BreakBlock(target, type, dropAllowed: !underTier);
    }

    private void BreakBlock(RaycastHit target, BlockType type, bool dropAllowed)
    {
        _world.SetBlock(target.X, target.Y, target.Z, BlockType.Air);
        _blockUpdater.NotifyBlockChanged(target.X, target.Y, target.Z);
        _progress = 0f;
        ActionPerformed?.Invoke();

        if (dropAllowed)
        {
            var drop = ItemInfo.GetDrop(type);
            if (drop != ItemType.None)
                _inventory.TryAdd(drop); // full inventory = the drop is lost
        }
    }

    private void TryPlace(RaycastHit target)
    {
        var stack = _inventory.SelectedStack;
        if (stack.IsEmpty || !ItemInfo.TryGetBlock(stack.Item, out var blockToPlace))
            return;

        int x = target.X + target.NormalX;
        int y = target.Y + target.NormalY;
        int z = target.Z + target.NormalZ;

        // Placing into water replaces it (there's no flow simulation). SetBlock
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
    }

    private bool HasAdjacentWater(int x, int y, int z) =>
        _world.GetBlock(x + 1, y, z) == BlockType.Water
        || _world.GetBlock(x - 1, y, z) == BlockType.Water
        || _world.GetBlock(x, y, z + 1) == BlockType.Water
        || _world.GetBlock(x, y, z - 1) == BlockType.Water;

    private bool IntersectsPlayer(int x, int y, int z)
    {
        var p = _player.Position;
        return x + 1 > p.X - PlayerPhysics.HalfWidth && x < p.X + PlayerPhysics.HalfWidth
            && y + 1 > p.Y && y < p.Y + PlayerPhysics.Height
            && z + 1 > p.Z - PlayerPhysics.HalfWidth && z < p.Z + PlayerPhysics.HalfWidth;
    }
}
