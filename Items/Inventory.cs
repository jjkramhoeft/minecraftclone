using System;

namespace MinecraftClone.Items;

/// <summary>
/// The player's items: 24 slots in a 6x4 grid. Slots 0–5 are the hotbar, so
/// hotbar keys and the scroll wheel select directly into the inventory.
/// </summary>
public class Inventory
{
    public const int Columns = 6;
    public const int Rows = 4;
    public const int Size = Columns * Rows;
    public const int HotbarSize = Columns;

    private readonly ItemStack[] _slots = new ItemStack[Size];
    private int _selectedIndex;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => _selectedIndex = Math.Clamp(value, 0, HotbarSize - 1);
    }

    public ItemStack SelectedStack => _slots[_selectedIndex];

    public ItemStack this[int slot]
    {
        get => _slots[slot];
        set => _slots[slot] = value;
    }

    /// <summary>Adds items, filling matching stacks first, then empty slots
    /// (hotbar first in both passes). False if nothing could be added at all;
    /// partial adds return true and drop the remainder.</summary>
    public bool TryAdd(ItemType item, int count = 1)
    {
        if (item == ItemType.None || count <= 0)
            return false;

        int remaining = count;
        int maxStack = ItemInfo.MaxStack(item);

        for (int i = 0; i < Size && remaining > 0; i++)
        {
            if (_slots[i].Item == item && _slots[i].Count < maxStack)
            {
                int moved = Math.Min(remaining, maxStack - _slots[i].Count);
                _slots[i].Count += moved;
                remaining -= moved;
            }
        }

        for (int i = 0; i < Size && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty)
            {
                int moved = Math.Min(remaining, maxStack);
                _slots[i] = new ItemStack(item, moved);
                remaining -= moved;
            }
        }

        return remaining < count;
    }

    public int CountOf(ItemType item)
    {
        int total = 0;
        foreach (var stack in _slots)
            if (stack.Item == item)
                total += stack.Count;
        return total;
    }

    /// <summary>True when there is room for at least one of the item.</summary>
    public bool HasRoomFor(ItemType item)
    {
        int maxStack = ItemInfo.MaxStack(item);
        foreach (var stack in _slots)
            if (stack.IsEmpty || (stack.Item == item && stack.Count < maxStack))
                return true;
        return false;
    }

    /// <summary>Removes count items of the type from anywhere in the inventory.
    /// All-or-nothing: consumes nothing unless the full count is available.</summary>
    public bool TryConsume(ItemType item, int count)
    {
        if (CountOf(item) < count)
            return false;

        int remaining = count;
        for (int i = Size - 1; i >= 0 && remaining > 0; i--) // non-hotbar slots first
        {
            if (_slots[i].Item != item)
                continue;
            int taken = Math.Min(remaining, _slots[i].Count);
            _slots[i].Count -= taken;
            if (_slots[i].Count <= 0)
                _slots[i] = ItemStack.Empty;
            remaining -= taken;
        }
        return true;
    }

    /// <summary>Removes one item from a specific slot (e.g. placing from the hand).</summary>
    public void ConsumeFromSlot(int slot, int count = 1)
    {
        _slots[slot].Count -= count;
        if (_slots[slot].Count <= 0)
            _slots[slot] = ItemStack.Empty;
    }
}
