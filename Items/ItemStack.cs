namespace MinecraftClone.Items;

/// <summary>A pile of one item type in an inventory slot. default = empty.</summary>
public struct ItemStack
{
    public ItemType Item;
    public int Count;

    public ItemStack(ItemType item, int count)
    {
        Item = item;
        Count = count;
    }

    public readonly bool IsEmpty => Item == ItemType.None || Count <= 0;

    public static readonly ItemStack Empty = default;
}
