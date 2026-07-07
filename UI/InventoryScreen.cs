using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Items;
using MinecraftClone.Rendering;

namespace MinecraftClone.UI;

/// <summary>
/// The full-inventory overlay (E key). While open the mouse is released and
/// look/interaction are suspended. Stacks are moved with the pick-up-on-cursor
/// model: click a slot to lift its stack, click again to drop/merge/swap.
/// </summary>
public class InventoryScreen
{
    private const int SlotPadding = 4;
    private const int HotbarGap = 14; // visual separation between storage rows and the hotbar row
    private const int RecipePanelWidth = 216;
    private const int RecipeRowHeight = 30;
    private const int RecipeIconSize = 24;

    private readonly Texture2D _pixel;
    private readonly Inventory _inventory;
    private ItemStack _held;

    public bool IsOpen { get; private set; }

    public InventoryScreen(GraphicsDevice device, Inventory inventory)
    {
        _inventory = inventory;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Toggle()
    {
        IsOpen = !IsOpen;
        if (!IsOpen && !_held.IsEmpty)
        {
            // Never void a lifted stack on close — it always fits back in
            // because it just came out of the inventory.
            _inventory.TryAdd(_held.Item, _held.Count);
            _held = ItemStack.Empty;
        }
    }

    public void Update(MouseState mouse, MouseState previousMouse, int screenWidth, int screenHeight)
    {
        bool click = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        if (!click)
            return;

        for (int i = 0; i < Recipes.All.Length; i++)
        {
            if (GetRecipeRowRect(i, screenWidth, screenHeight).Contains(mouse.Position))
            {
                if (_held.IsEmpty) // don't craft with a lifted stack — it isn't counted
                    Recipes.TryCraft(Recipes.All[i], _inventory);
                return;
            }
        }

        for (int slot = 0; slot < Inventory.Size; slot++)
        {
            if (!GetSlotRect(slot, screenWidth, screenHeight).Contains(mouse.Position))
                continue;

            var inSlot = _inventory[slot];
            if (_held.IsEmpty)
            {
                _held = inSlot;
                _inventory[slot] = ItemStack.Empty;
            }
            else if (inSlot.IsEmpty)
            {
                _inventory[slot] = _held;
                _held = ItemStack.Empty;
            }
            else if (inSlot.Item == _held.Item)
            {
                int maxStack = ItemInfo.MaxStack(inSlot.Item);
                int moved = System.Math.Min(_held.Count, maxStack - inSlot.Count);
                inSlot.Count += moved;
                _inventory[slot] = inSlot;
                _held.Count -= moved;
                if (_held.Count <= 0)
                    _held = ItemStack.Empty;
            }
            else
            {
                (_held, _inventory[slot]) = (inSlot, _held);
            }
            return;
        }
    }

    public void Draw(SpriteBatch spriteBatch, TextureAtlas atlas, PixelFont font, MouseState mouse, int screenWidth, int screenHeight)
    {
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        // Dim the world, frame the panel.
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight), new Color(0, 0, 0, 120));
        var panel = GetPanelRect(screenWidth, screenHeight);
        spriteBatch.Draw(_pixel, panel, new Color(28, 28, 28, 235));

        for (int slot = 0; slot < Inventory.Size; slot++)
        {
            SlotRenderer.Draw(spriteBatch, _pixel, atlas, font,
                GetSlotRect(slot, screenWidth, screenHeight), _inventory[slot],
                slot == _inventory.SelectedIndex);
        }

        DrawRecipes(spriteBatch, atlas, font, screenWidth, screenHeight);

        if (!_held.IsEmpty)
        {
            var heldRect = new Rectangle(mouse.X - SlotRenderer.SlotSize / 2, mouse.Y - SlotRenderer.SlotSize / 2,
                SlotRenderer.SlotSize, SlotRenderer.SlotSize);
            SlotRenderer.Draw(spriteBatch, _pixel, atlas, font, heldRect, _held, false);
        }

        spriteBatch.End();
    }

    private void DrawRecipes(SpriteBatch spriteBatch, TextureAtlas atlas, PixelFont font, int screenWidth, int screenHeight)
    {
        for (int i = 0; i < Recipes.All.Length; i++)
        {
            var recipe = Recipes.All[i];
            var row = GetRecipeRowRect(i, screenWidth, screenHeight);
            bool affordable = Recipes.CanAfford(recipe, _inventory);
            var tint = affordable ? Color.White : new Color(255, 255, 255, 70);

            spriteBatch.Draw(_pixel, row, new Color(0, 0, 0, affordable ? 150 : 60));

            // Output: icon xN, then a gap, then each input icon xN.
            int x = row.X + 4;
            int iconY = row.Y + (row.Height - RecipeIconSize) / 2;
            DrawItemIcon(spriteBatch, atlas, recipe.Output.Item, new Rectangle(x, iconY, RecipeIconSize, RecipeIconSize), tint);
            x += RecipeIconSize + 1;
            if (recipe.Output.Count > 1)
            {
                font.Draw(spriteBatch, "x" + recipe.Output.Count, x, row.Y + row.Height / 2 - PixelFont.GlyphHeight, 2, tint);
                x += PixelFont.MeasureWidth("x" + recipe.Output.Count, 2);
            }
            x += 14;

            foreach (var (item, count) in recipe.Inputs)
            {
                DrawItemIcon(spriteBatch, atlas, item, new Rectangle(x, iconY, RecipeIconSize, RecipeIconSize), tint);
                x += RecipeIconSize + 1;
                font.Draw(spriteBatch, "x" + count, x, row.Y + row.Height / 2 - PixelFont.GlyphHeight, 2, tint);
                x += PixelFont.MeasureWidth("x" + count, 2) + 6;
            }
        }
    }

    private static void DrawItemIcon(SpriteBatch spriteBatch, TextureAtlas atlas, ItemType item, Rectangle rect, Color tint)
    {
        int tile = ItemInfo.GetIconTile(item);
        var source = new Rectangle(
            tile % TextureAtlas.TilesPerRow * TextureAtlas.TileSize,
            tile / TextureAtlas.TilesPerRow * TextureAtlas.TileSize,
            TextureAtlas.TileSize, TextureAtlas.TileSize);
        spriteBatch.Draw(atlas.Texture, rect, source, tint);
    }

    private static Rectangle GetPanelRect(int screenWidth, int screenHeight)
    {
        int width = Inventory.Columns * (SlotRenderer.SlotSize + SlotPadding) - SlotPadding + RecipePanelWidth + 48;
        int gridHeight = Inventory.Rows * (SlotRenderer.SlotSize + SlotPadding) - SlotPadding + HotbarGap + 32;
        int recipeHeight = Recipes.All.Length * (RecipeRowHeight + 4) + 28;
        int height = System.Math.Max(gridHeight, recipeHeight);
        return new Rectangle((screenWidth - width) / 2, (screenHeight - height) / 2, width, height);
    }

    private static Rectangle GetRecipeRowRect(int index, int screenWidth, int screenHeight)
    {
        var panel = GetPanelRect(screenWidth, screenHeight);
        int x = panel.Right - RecipePanelWidth - 16;
        int y = panel.Y + 16 + index * (RecipeRowHeight + 4);
        return new Rectangle(x, y, RecipePanelWidth, RecipeRowHeight);
    }

    /// <summary>Slots 6-23 are the three storage rows (top); slots 0-5 (the
    /// hotbar) sit below them with a gap.</summary>
    private static Rectangle GetSlotRect(int slot, int screenWidth, int screenHeight)
    {
        var panel = GetPanelRect(screenWidth, screenHeight);
        int column = slot % Inventory.Columns;
        bool isHotbar = slot < Inventory.HotbarSize;
        int displayRow = isHotbar ? Inventory.Rows - 1 : slot / Inventory.Columns - 1;

        int x = panel.X + 16 + column * (SlotRenderer.SlotSize + SlotPadding);
        int y = panel.Y + 16 + displayRow * (SlotRenderer.SlotSize + SlotPadding) + (isHotbar ? HotbarGap : 0);
        return new Rectangle(x, y, SlotRenderer.SlotSize, SlotRenderer.SlotSize);
    }
}
