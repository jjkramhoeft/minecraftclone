using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.UI;

/// <summary>
/// F3 stats panel: a few PixelFont lines over a translucent backdrop in the
/// top-left corner. The lines are rebuilt once per second by MainGame (same
/// cadence as the window title), so drawing every frame allocates nothing.
/// </summary>
public class DebugOverlay
{
    private const int TextScale = 2;
    private const int Padding = 8;
    private const int LineSpacing = 4;

    public bool Visible;

    /// <summary>Rebuilt once per second; null entries are skipped.</summary>
    public readonly string[] Lines = new string[6];

    private readonly Texture2D _pixel;

    public DebugOverlay(GraphicsDevice device)
    {
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Draw(SpriteBatch spriteBatch, PixelFont font)
    {
        if (!Visible)
            return;

        int lineHeight = PixelFont.GlyphHeight * TextScale + LineSpacing;
        int maxWidth = 0, count = 0;
        foreach (var line in Lines)
        {
            if (line == null)
                continue;
            count++;
            maxWidth = System.Math.Max(maxWidth, PixelFont.MeasureWidth(line, TextScale));
        }
        if (count == 0)
            return;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        spriteBatch.Draw(_pixel,
            new Rectangle(Padding - 4, Padding - 4, maxWidth + 8, count * lineHeight - LineSpacing + 8),
            new Color(0, 0, 0, 110));
        int y = Padding;
        foreach (var line in Lines)
        {
            if (line == null)
                continue;
            font.Draw(spriteBatch, line, Padding, y, TextScale, Color.White);
            y += lineHeight;
        }
        spriteBatch.End();
    }
}
