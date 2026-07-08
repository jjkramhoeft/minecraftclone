namespace MinecraftClone.Items;

/// <summary>A shapeless recipe: consume the inputs, receive the output.</summary>
public record Recipe(ItemStack Output, (ItemType Item, int Count)[] Inputs);

public static class Recipes
{
    public static readonly Recipe[] All =
    {
        new(new ItemStack(ItemType.Planks, 4), new[] { (ItemType.Wood, 1) }),
        new(new ItemStack(ItemType.Stick, 4), new[] { (ItemType.Planks, 2) }),
        new(new ItemStack(ItemType.Bricks, 4), new[] { (ItemType.Sand, 4) }),
        new(new ItemStack(ItemType.WoodenPickaxe, 1), new[] { (ItemType.Planks, 3), (ItemType.Stick, 2) }),
        new(new ItemStack(ItemType.WoodenAxe, 1), new[] { (ItemType.Planks, 3), (ItemType.Stick, 2) }),
        new(new ItemStack(ItemType.WoodenShovel, 1), new[] { (ItemType.Planks, 1), (ItemType.Stick, 2) }),
        new(new ItemStack(ItemType.StonePickaxe, 1), new[] { (ItemType.Stone, 3), (ItemType.Stick, 2) }),
        new(new ItemStack(ItemType.StoneAxe, 1), new[] { (ItemType.Stone, 3), (ItemType.Stick, 2) }),
        new(new ItemStack(ItemType.StoneShovel, 1), new[] { (ItemType.Stone, 1), (ItemType.Stick, 2) }),
        // Placeholder until the furnace exists: "smelt" ore by hand.
        new(new ItemStack(ItemType.IronIngot, 1), new[] { (ItemType.IronOre, 1), (ItemType.Coal, 1) }),
        new(new ItemStack(ItemType.IronPickaxe, 1), new[] { (ItemType.IronIngot, 3), (ItemType.Stick, 2) }),
        new(new ItemStack(ItemType.IronAxe, 1), new[] { (ItemType.IronIngot, 3), (ItemType.Stick, 2) }),
        new(new ItemStack(ItemType.IronShovel, 1), new[] { (ItemType.IronIngot, 1), (ItemType.Stick, 2) }),
    };

    /// <summary>Inputs available (room for the output is only checked by TryCraft).</summary>
    public static bool CanAfford(Recipe recipe, Inventory inventory)
    {
        foreach (var (item, count) in recipe.Inputs)
            if (inventory.CountOf(item) < count)
                return false;
        return true;
    }

    /// <summary>All-or-nothing: if the output doesn't fully fit after the inputs
    /// are consumed, the inventory is rolled back and nothing happens.</summary>
    public static bool TryCraft(Recipe recipe, Inventory inventory)
    {
        if (!CanAfford(recipe, inventory))
            return false;

        var backup = inventory.SnapshotSlots();
        foreach (var (item, count) in recipe.Inputs)
            inventory.TryConsume(item, count);

        inventory.TryAdd(recipe.Output.Item, recipe.Output.Count);
        if (inventory.CountOf(recipe.Output.Item) - CountIn(backup, recipe.Output.Item) < recipe.Output.Count)
        {
            inventory.RestoreSlots(backup);
            return false;
        }
        return true;
    }

    private static int CountIn(ItemStack[] slots, ItemType item)
    {
        int total = 0;
        foreach (var stack in slots)
            if (stack.Item == item)
                total += stack.Count;
        return total;
    }
}
