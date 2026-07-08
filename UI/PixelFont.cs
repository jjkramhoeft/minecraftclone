using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.UI;

/// <summary>
/// Minimal procedural bitmap font (3x5 pixels per glyph), baked to a tiny
/// texture at startup — the project deliberately has no SpriteFont/content
/// pipeline. Digits, A-Z (input is uppercased), and light punctuation.
/// Lowercase 'x' stays a dedicated glyph for stack counts ("x64").
/// </summary>
public class PixelFont
{
    public const int GlyphWidth = 3;
    public const int GlyphHeight = 5;
    private const int CellWidth = GlyphWidth + 1; // 1px spacing baked into the texture

    private const string Charset = "0123456789xABCDEFGHIJKLMNOPQRSTUVWXYZ.-:!";

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
        new[] { "010", "101", "111", "101", "101" }, // A
        new[] { "110", "101", "110", "101", "110" }, // B
        new[] { "011", "100", "100", "100", "011" }, // C
        new[] { "110", "101", "101", "101", "110" }, // D
        new[] { "111", "100", "110", "100", "111" }, // E
        new[] { "111", "100", "110", "100", "100" }, // F
        new[] { "011", "100", "101", "101", "011" }, // G
        new[] { "101", "101", "111", "101", "101" }, // H
        new[] { "111", "010", "010", "010", "111" }, // I
        new[] { "011", "001", "001", "101", "010" }, // J
        new[] { "101", "110", "100", "110", "101" }, // K
        new[] { "100", "100", "100", "100", "111" }, // L
        new[] { "101", "111", "111", "101", "101" }, // M
        new[] { "110", "101", "101", "101", "101" }, // N
        new[] { "010", "101", "101", "101", "010" }, // O
        new[] { "110", "101", "110", "100", "100" }, // P
        new[] { "010", "101", "101", "110", "011" }, // Q
        new[] { "110", "101", "110", "101", "101" }, // R
        new[] { "011", "100", "010", "001", "110" }, // S
        new[] { "111", "010", "010", "010", "010" }, // T
        new[] { "101", "101", "101", "101", "111" }, // U
        new[] { "101", "101", "101", "101", "010" }, // V
        new[] { "101", "101", "111", "111", "101" }, // W
        new[] { "101", "101", "010", "101", "101" }, // X
        new[] { "101", "101", "010", "010", "010" }, // Y
        new[] { "111", "001", "010", "100", "111" }, // Z
        new[] { "000", "000", "000", "000", "010" }, // .
        new[] { "000", "000", "111", "000", "000" }, // -
        new[] { "000", "010", "000", "010", "000" }, // :
        new[] { "010", "010", "010", "000", "010" }, // !
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

    /// <summary>Draws text; letters are uppercased ('x' stays the count glyph),
    /// unknown chars (including spaces) are skipped as spaces.</summary>
    public void Draw(SpriteBatch spriteBatch, string text, int x, int y, int scale, Color color)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c != 'x' && char.IsAsciiLetterLower(c))
                c = char.ToUpperInvariant(c);
            int glyph = Charset.IndexOf(c);
            if (glyph >= 0)
            {
                var source = new Rectangle(glyph * CellWidth, 0, GlyphWidth, GlyphHeight);
                var dest = new Rectangle(x + i * CellWidth * scale, y, GlyphWidth * scale, GlyphHeight * scale);
                spriteBatch.Draw(_texture, dest, source, color);
            }
        }
    }
}
