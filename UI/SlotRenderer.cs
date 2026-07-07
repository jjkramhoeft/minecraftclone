using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.Items;
using MinecraftClone.Rendering;

namespace MinecraftClone.UI;

/// <summary>Draws one inventory slot — shared by the hotbar and the inventory screen.</summary>
public static class SlotRenderer
{
    public const int SlotSize = 48;
    private const int IconSize = 40;

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, TextureAtlas atlas, PixelFont font,
        Rectangle rect, ItemStack stack, bool selected)
    {
        spriteBatch.Draw(pixel, rect, selected ? Color.White : new Color(0, 0, 0, 150));
        var inner = new Rectangle(rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6);
        spriteBatch.Draw(pixel, inner, new Color(45, 45, 45, 220));

        if (stack.IsEmpty)
            return;

        int tile = ItemInfo.GetIconTile(stack.Item);
        var source = new Rectangle(
            tile % TextureAtlas.TilesPerRow * TextureAtlas.TileSize,
            tile / TextureAtlas.TilesPerRow * TextureAtlas.TileSize,
            TextureAtlas.TileSize, TextureAtlas.TileSize);
        var iconRect = new Rectangle(
            rect.X + (rect.Width - IconSize) / 2,
            rect.Y + (rect.Height - IconSize) / 2,
            IconSize, IconSize);
        spriteBatch.Draw(atlas.Texture, iconRect, source, Color.White);

        if (stack.Count > 1)
        {
            string count = stack.Count.ToString();
            int textWidth = PixelFont.MeasureWidth(count, 2);
            font.Draw(spriteBatch, count, rect.Right - textWidth - 3, rect.Bottom - PixelFont.GlyphHeight * 2 - 4, 2, Color.White);
        }
    }
}
