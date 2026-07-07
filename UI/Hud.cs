using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.UI;

/// <summary>2D overlay: crosshair now, hotbar and debug text later.</summary>
public class Hud
{
    private readonly Texture2D _pixel;

    public Hud(GraphicsDevice device)
    {
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        int cx = screenWidth / 2, cy = screenHeight / 2;
        var color = new Color(255, 255, 255, 190);

        spriteBatch.Begin();
        spriteBatch.Draw(_pixel, new Rectangle(cx - 8, cy - 1, 16, 2), color);
        spriteBatch.Draw(_pixel, new Rectangle(cx - 1, cy - 8, 2, 16), color);
        spriteBatch.End();
    }
}
