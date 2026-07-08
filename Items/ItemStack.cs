namespace MinecraftClone.Items;

/// <summary>A pile of one item type in an inventory slot. default = empty.</summary>
public struct ItemStack
{
    public ItemType Item;
    public int Count;

    /// <summary>Wear on a tool (0 = fresh). Only meaningful for items with a
    /// max durability; the tool is destroyed when it reaches that value.</summary>
    public int Damage;

    public ItemStack(ItemType item, int count)
    {
        Item = item;
        Count = count;
        Damage = 0;
    }

    public readonly bool IsEmpty => Item == ItemType.None || Count <= 0;

    public static readonly ItemStack Empty = default;
}
