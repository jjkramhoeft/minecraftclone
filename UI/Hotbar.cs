using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Items;
using MinecraftClone.Rendering;

namespace MinecraftClone.UI;

/// <summary>
/// A view over the first 6 inventory slots: keys 1-6 or the scroll wheel pick
/// the active slot, and right-click places from it.
/// </summary>
public class Hotbar
{
    private static readonly Keys[] SlotKeys =
    {
        Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6,
    };

    private const int SlotPadding = 4;

    private readonly Texture2D _pixel;
    private readonly Inventory _inventory;
    private int _previousScroll;

    public Hotbar(GraphicsDevice device, Inventory inventory)
    {
        _inventory = inventory;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Update(KeyboardState keyboard, MouseState mouse)
    {
        for (int i = 0; i < SlotKeys.Length; i++)
            if (keyboard.IsKeyDown(SlotKeys[i]))
                _inventory.SelectedIndex = i;

        int scrollDelta = mouse.ScrollWheelValue - _previousScroll;
        _previousScroll = mouse.ScrollWheelValue;
        if (scrollDelta < 0)
            _inventory.SelectedIndex = (_inventory.SelectedIndex + 1) % Inventory.HotbarSize;
        else if (scrollDelta > 0)
            _inventory.SelectedIndex = (_inventory.SelectedIndex + Inventory.HotbarSize - 1) % Inventory.HotbarSize;
    }

    public void Draw(SpriteBatch spriteBatch, TextureAtlas atlas, PixelFont font, int screenWidth, int screenHeight)
    {
        int totalWidth = Inventory.HotbarSize * SlotRenderer.SlotSize + (Inventory.HotbarSize - 1) * SlotPadding;
        int x0 = (screenWidth - totalWidth) / 2;
        int y0 = screenHeight - SlotRenderer.SlotSize - 12;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            var rect = new Rectangle(x0 + i * (SlotRenderer.SlotSize + SlotPadding), y0, SlotRenderer.SlotSize, SlotRenderer.SlotSize);
            SlotRenderer.Draw(spriteBatch, _pixel, atlas, font, rect, _inventory[i], i == _inventory.SelectedIndex);
        }
        spriteBatch.End();
    }
}
