using System.Collections.Generic;

namespace MinecraftClone.Items;

/// <summary>
/// Per-position chest storage: the first container block. Contents live
/// outside the chunk byte array, keyed by world position, and are saved in
/// world metadata. Breaking a chest pours its contents at the player.
/// </summary>
public class Chests
{
    public const int ChestSize = 12; // 6x2 grid

    private readonly Dictionary<(int X, int Y, int Z), ItemStack[]> _contents = new();

    public IReadOnlyDictionary<(int X, int Y, int Z), ItemStack[]> All => _contents;

    public void Clear() => _contents.Clear();

    /// <summary>The chest's slot array, created on first open. The array is
    /// shared with the chest screen, so edits there persist automatically.</summary>
    public ItemStack[] GetOrCreate(int x, int y, int z)
    {
        if (!_contents.TryGetValue((x, y, z), out var slots))
            _contents[(x, y, z)] = slots = new ItemStack[ChestSize];
        return slots;
    }

    public void Restore(int x, int y, int z, ItemStack[] slots) => _contents[(x, y, z)] = slots;

    /// <summary>Hands the contents to the player (best effort; overflow is
    /// lost like any other drop) and forgets the chest.</summary>
    public void OnBroken(Inventory inventory, int x, int y, int z)
    {
        if (!_contents.Remove((x, y, z), out var slots))
            return;
        foreach (var stack in slots)
            if (!stack.IsEmpty)
                inventory.TryAdd(stack.Item, stack.Count);
    }
}
