using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Items;
using MinecraftClone.Rendering;

namespace MinecraftClone.UI;

/// <summary>
/// The open-chest overlay: the chest's 6x2 grid on top, the player's full
/// inventory below, with the same lift/drop/merge/swap click model as the
/// inventory screen. The chest slot array is shared with the Chests store, so
/// every change persists without an explicit save step.
/// </summary>
public class ChestScreen
{
    private const int SlotPadding = 4;
    private const int SectionGap = 18; // between the chest grid and the player grid

    private readonly Texture2D _pixel;
    private readonly Inventory _inventory;
    private ItemStack[] _chest; // null while closed
    private ItemStack _held;

    public bool IsOpen => _chest != null;

    public ChestScreen(GraphicsDevice device, Inventory inventory)
    {
        _inventory = inventory;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Open(ItemStack[] chestSlots) => _chest = chestSlots;

    public void Close()
    {
        if (!_held.IsEmpty)
        {
            // Never void a lifted stack — it always fits back where it came from.
            _inventory.TryAdd(_held.Item, _held.Count);
            _held = ItemStack.Empty;
        }
        _chest = null;
    }

    public void Update(MouseState mouse, MouseState previousMouse, int screenWidth, int screenHeight)
    {
        bool click = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        if (!click || !IsOpen)
            return;

        for (int i = 0; i < Chests.ChestSize; i++)
        {
            if (ChestSlotRect(i, screenWidth, screenHeight).Contains(mouse.Position))
            {
                ClickSlot(ref _chest[i]);
                return;
            }
        }
        for (int slot = 0; slot < Inventory.Size; slot++)
        {
            if (InventorySlotRect(slot, screenWidth, screenHeight).Contains(mouse.Position))
            {
                var stack = _inventory[slot];
                ClickSlot(ref stack);
                _inventory[slot] = stack;
                return;
            }
        }
    }

    private void ClickSlot(ref ItemStack inSlot)
    {
        if (_held.IsEmpty)
        {
            _held = inSlot;
            inSlot = ItemStack.Empty;
        }
        else if (inSlot.IsEmpty)
        {
            inSlot = _held;
            _held = ItemStack.Empty;
        }
        else if (inSlot.Item == _held.Item)
        {
            int maxStack = ItemInfo.MaxStack(inSlot.Item);
            int moved = System.Math.Min(_held.Count, maxStack - inSlot.Count);
            inSlot.Count += moved;
            _held.Count -= moved;
            if (_held.Count <= 0)
                _held = ItemStack.Empty;
        }
        else
        {
            (_held, inSlot) = (inSlot, _held);
        }
    }

    public void Draw(SpriteBatch spriteBatch, TextureAtlas atlas, PixelFont font, MouseState mouse, int screenWidth, int screenHeight)
    {
        if (!IsOpen)
            return;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight), new Color(0, 0, 0, 120));
        var panel = PanelRect(screenWidth, screenHeight);
        spriteBatch.Draw(_pixel, panel, new Color(28, 28, 28, 235));

        font.Draw(spriteBatch, "CHEST", panel.X + 16, panel.Y + 6, 2, new Color(200, 200, 200));

        for (int i = 0; i < Chests.ChestSize; i++)
            SlotRenderer.Draw(spriteBatch, _pixel, atlas, font,
                ChestSlotRect(i, screenWidth, screenHeight), _chest[i], false);

        for (int slot = 0; slot < Inventory.Size; slot++)
            SlotRenderer.Draw(spriteBatch, _pixel, atlas, font,
                InventorySlotRect(slot, screenWidth, screenHeight), _inventory[slot],
                slot == _inventory.SelectedIndex);

        if (!_held.IsEmpty)
        {
            var heldRect = new Rectangle(mouse.X - SlotRenderer.SlotSize / 2, mouse.Y - SlotRenderer.SlotSize / 2,
                SlotRenderer.SlotSize, SlotRenderer.SlotSize);
            SlotRenderer.Draw(spriteBatch, _pixel, atlas, font, heldRect, _held, false);
        }

        spriteBatch.End();
    }

    private const int ChestRows = 2;

    private static Rectangle PanelRect(int screenWidth, int screenHeight)
    {
        int width = Inventory.Columns * (SlotRenderer.SlotSize + SlotPadding) - SlotPadding + 32;
        int height = (ChestRows + Inventory.Rows) * (SlotRenderer.SlotSize + SlotPadding) - SlotPadding
            + SectionGap + 40;
        return new Rectangle((screenWidth - width) / 2, (screenHeight - height) / 2, width, height);
    }

    private static Rectangle ChestSlotRect(int index, int screenWidth, int screenHeight)
    {
        var panel = PanelRect(screenWidth, screenHeight);
        int x = panel.X + 16 + index % Inventory.Columns * (SlotRenderer.SlotSize + SlotPadding);
        int y = panel.Y + 24 + index / Inventory.Columns * (SlotRenderer.SlotSize + SlotPadding);
        return new Rectangle(x, y, SlotRenderer.SlotSize, SlotRenderer.SlotSize);
    }

    /// <summary>Player grid below the chest: storage rows first, hotbar last —
    /// same visual order as the inventory screen.</summary>
    private static Rectangle InventorySlotRect(int slot, int screenWidth, int screenHeight)
    {
        var panel = PanelRect(screenWidth, screenHeight);
        int column = slot % Inventory.Columns;
        bool isHotbar = slot < Inventory.HotbarSize;
        int displayRow = isHotbar ? Inventory.Rows - 1 : slot / Inventory.Columns - 1;

        int x = panel.X + 16 + column * (SlotRenderer.SlotSize + SlotPadding);
        int y = panel.Y + 24 + ChestRows * (SlotRenderer.SlotSize + SlotPadding) + SectionGap
            + displayRow * (SlotRenderer.SlotSize + SlotPadding);
        return new Rectangle(x, y, SlotRenderer.SlotSize, SlotRenderer.SlotSize);
    }
}
