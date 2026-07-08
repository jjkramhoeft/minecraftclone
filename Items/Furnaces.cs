using System.Collections.Generic;
using MinecraftClone.World;

namespace MinecraftClone.Items;

/// <summary>
/// Per-position furnace state — the first block with state and a timer. A
/// smelt consumes the held smeltable plus one coal, swaps the block to
/// FurnaceLit (which glows via the light engine), and after a few seconds the
/// output waits inside until the player right-clicks to collect it. State
/// lives outside the chunk byte array and is saved in world metadata.
/// </summary>
public class Furnaces
{
    public class State
    {
        public ItemType Output;
        public int OutputCount;
        public float SecondsRemaining; // > 0 while smelting
    }

    private const float SmeltSeconds = 3f;

    private readonly Dictionary<(int X, int Y, int Z), State> _states = new();
    private readonly List<(int X, int Y, int Z)> _finished = new();

    public IReadOnlyDictionary<(int X, int Y, int Z), State> States => _states;

    /// <summary>What smelting one of this item yields, or None.</summary>
    public static ItemType SmeltResultFor(ItemType input) => input switch
    {
        ItemType.IronOre => ItemType.IronIngot,
        ItemType.Sand => ItemType.Glass,
        _ => ItemType.None,
    };

    public void Clear() => _states.Clear();

    /// <summary>Restores a saved state (used on world load).</summary>
    public void Restore(int x, int y, int z, ItemType output, int count, float secondsRemaining) =>
        _states[(x, y, z)] = new State { Output = output, OutputCount = count, SecondsRemaining = secondsRemaining };

    /// <summary>Right-click behavior. Collects a finished output first;
    /// otherwise starts a smelt if the held item smelts and coal is available.
    /// Returns true when the click was absorbed by the furnace.</summary>
    public bool Use(ChunkManager world, Inventory inventory, int x, int y, int z)
    {
        var key = (x, y, z);
        if (_states.TryGetValue(key, out var state))
        {
            if (state.SecondsRemaining <= 0f && state.OutputCount > 0
                && inventory.TryAdd(state.Output, state.OutputCount))
                _states.Remove(key);
            return true; // busy or output stuck (inventory full) — click absorbed
        }

        var held = inventory.SelectedStack.Item;
        var result = SmeltResultFor(held);
        if (result == ItemType.None || inventory.CountOf(ItemType.Coal) < 1)
            return false;

        inventory.ConsumeFromSlot(inventory.SelectedIndex);
        inventory.TryConsume(ItemType.Coal, 1);
        _states[key] = new State { Output = result, OutputCount = 1, SecondsRemaining = SmeltSeconds };
        world.SetBlock(x, y, z, BlockType.FurnaceLit);
        return true;
    }

    /// <summary>Ticks smelts; finished furnaces swap back to the unlit block
    /// (which also removes their light) and hold the output for pickup.</summary>
    public void Update(ChunkManager world, float dt)
    {
        _finished.Clear();
        foreach (var (pos, state) in _states)
        {
            if (state.SecondsRemaining <= 0f)
                continue;
            state.SecondsRemaining -= dt;
            if (state.SecondsRemaining <= 0f)
            {
                state.SecondsRemaining = 0f;
                _finished.Add(pos);
            }
        }
        foreach (var (x, y, z) in _finished)
        {
            if (world.GetBlock(x, y, z) == BlockType.FurnaceLit)
                world.SetBlock(x, y, z, BlockType.Furnace);
        }
    }

    /// <summary>A broken furnace hands any stored/pending output to the player
    /// (best effort) and forgets its state.</summary>
    public void OnBroken(Inventory inventory, int x, int y, int z)
    {
        if (_states.Remove((x, y, z), out var state) && state.OutputCount > 0)
            inventory.TryAdd(state.Output, state.OutputCount);
    }
}
