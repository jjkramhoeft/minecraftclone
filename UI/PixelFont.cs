using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.UI;

/// <summary>
/// Minimal procedural bitmap font (3x5 pixels per glyph), baked to a tiny
/// texture at startup — the project deliberately has no SpriteFont/content
/// pipeline. Supports exactly what the UI needs: digits and 'x'.
/// </summary>
public class PixelFont
{
    public const int GlyphWidth = 3;
    public const int GlyphHeight = 5;
    private const int CellWidth = GlyphWidth + 1; // 1px spacing baked into the texture

    private const string Charset = "0123456789x";

    // 3x5 glyph bitmaps, one string per row.
    private static readonly string[][] Glyphs =
    {
        new[] { "111", "101", "101", "101", "111" }, // 0
        new[] { "010", "110", "010", "010", "111" }, // 1
        new[] { "111", "001", "111", "100", "111" }, // 2
        new[] { "111", "001", "111", "001", "111" }, // 3
        new[] { "101", "101", "111", "001", "001" }, // 4
        new[] { "111", "100", "111", "001", "111" }, // 5
        new[] { "111", "100", "111", "101", "111" }, // 6
        new[] { "111", "001", "001", "010", "010" }, // 7
        new[] { "111", "101", "111", "101", "111" }, // 8
        new[] { "111", "101", "111", "001", "111" }, // 9
        new[] { "000", "101", "010", "101", "000" }, // x
    };

    private readonly Texture2D _texture;

    public PixelFont(GraphicsDevice device)
    {
        var pixels = new Color[Charset.Length * CellWidth * GlyphHeight];
        for (int g = 0; g < Charset.Length; g++)
            for (int y = 0; y < GlyphHeight; y++)
                for (int x = 0; x < GlyphWidth; x++)
                    if (Glyphs[g][y][x] == '1')
                        pixels[g * CellWidth + x + y * Charset.Length * CellWidth] = Color.White;

        _texture = new Texture2D(device, Charset.Length * CellWidth, GlyphHeight);
        _texture.SetData(pixels);
    }

    public static int MeasureWidth(string text, int scale) => text.Length * CellWidth * scale;

    /// <summary>Draws text containing only charset glyphs; unknown chars are skipped as spaces.</summary>
    public void Draw(SpriteBatch spriteBatch, string text, int x, int y, int scale, Color color)
    {
        for (int i = 0; i < text.Length; i++)
        {
            int glyph = Charset.IndexOf(text[i]);
            if (glyph >= 0)
            {
                var source = new Rectangle(glyph * CellWidth, 0, GlyphWidth, GlyphHeight);
                var dest = new Rectangle(x + i * CellWidth * scale, y, GlyphWidth * scale, GlyphHeight * scale);
                spriteBatch.Draw(_texture, dest, source, color);
            }
        }
    }
}
