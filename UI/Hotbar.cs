using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Rendering;
using MinecraftClone.World;

namespace MinecraftClone.UI;

/// <summary>
/// Selects which block right-click places: keys 1-6 or the scroll wheel.
/// Drawn as a row of slots along the bottom, each showing its block's atlas tile.
/// </summary>
public class Hotbar
{
    private static readonly BlockType[] Slots =
    {
        BlockType.Grass, BlockType.Dirt, BlockType.Stone,
        BlockType.Sand, BlockType.Wood, BlockType.Leaves,
    };

    private static readonly Keys[] SlotKeys =
    {
        Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6,
    };

    private const int SlotSize = 48;
    private const int SlotPadding = 4;
    private const int IconSize = 40;

    private readonly Texture2D _pixel;
    private int _previousScroll;

    public int SelectedIndex { get; private set; }
    public BlockType SelectedBlock => Slots[SelectedIndex];

    public void Select(int index) => SelectedIndex = Math.Clamp(index, 0, Slots.Length - 1);

    public Hotbar(GraphicsDevice device)
    {
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Update(KeyboardState keyboard, MouseState mouse)
    {
        for (int i = 0; i < SlotKeys.Length; i++)
            if (keyboard.IsKeyDown(SlotKeys[i]))
                SelectedIndex = i;

        int scrollDelta = mouse.ScrollWheelValue - _previousScroll;
        _previousScroll = mouse.ScrollWheelValue;
        if (scrollDelta < 0)
            SelectedIndex = (SelectedIndex + 1) % Slots.Length;
        else if (scrollDelta > 0)
            SelectedIndex = (SelectedIndex + Slots.Length - 1) % Slots.Length;
    }

    public void Draw(SpriteBatch spriteBatch, TextureAtlas atlas, int screenWidth, int screenHeight)
    {
        int totalWidth = Slots.Length * SlotSize + (Slots.Length - 1) * SlotPadding;
        int x0 = (screenWidth - totalWidth) / 2;
        int y0 = screenHeight - SlotSize - 12;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        for (int i = 0; i < Slots.Length; i++)
        {
            var slotRect = new Rectangle(x0 + i * (SlotSize + SlotPadding), y0, SlotSize, SlotSize);
            spriteBatch.Draw(_pixel, slotRect, i == SelectedIndex ? Color.White : new Color(0, 0, 0, 150));

            var innerRect = new Rectangle(slotRect.X + 3, slotRect.Y + 3, SlotSize - 6, SlotSize - 6);
            spriteBatch.Draw(_pixel, innerRect, new Color(45, 45, 45, 220));

            // The side face is the most recognizable (grass shows its dirt+grass edge).
            int tile = BlockInfo.GetFaceTile(Slots[i], BlockFace.South);
            var source = new Rectangle(
                tile % TextureAtlas.TilesPerRow * TextureAtlas.TileSize,
                tile / TextureAtlas.TilesPerRow * TextureAtlas.TileSize,
                TextureAtlas.TileSize, TextureAtlas.TileSize);
            var iconRect = new Rectangle(
                slotRect.X + (SlotSize - IconSize) / 2,
                slotRect.Y + (SlotSize - IconSize) / 2,
                IconSize, IconSize);
            spriteBatch.Draw(atlas.Texture, iconRect, source, Color.White);
        }
        spriteBatch.End();
    }
}
