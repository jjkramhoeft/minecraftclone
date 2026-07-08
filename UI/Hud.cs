using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.Items;
using MinecraftClone.Player;

namespace MinecraftClone.UI;

/// <summary>2D overlay: crosshair, hearts, and air bubbles. Sprites are tiny
/// procedural pixel-art textures generated at startup.</summary>
public class Hud
{
    private const int SpriteScale = 3;

    private readonly Texture2D _pixel;
    private readonly Texture2D _heart;
    private readonly Texture2D _bubble;

    public Hud(GraphicsDevice device)
    {
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _heart = MakeSprite(device, new[]
        {
            ".XX.XX.",
            "XXXXXXX",
            "XXXXXXX",
            ".XXXXX.",
            "..XXX..",
            "...X...",
            ".......",
        });
        _bubble = MakeSprite(device, new[]
        {
            ".XXXX.",
            "X....X",
            "X.X..X",
            "X....X",
            "X....X",
            ".XXXX.",
        });
    }

    private static Texture2D MakeSprite(GraphicsDevice device, string[] rows)
    {
        int w = rows[0].Length, h = rows.Length;
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[x + y * w] = rows[y][x] == 'X' ? Color.White : Color.Transparent;
        var texture = new Texture2D(device, w, h);
        texture.SetData(pixels);
        return texture;
    }

    public void Draw(SpriteBatch spriteBatch, PlayerHealth health, int screenWidth, int screenHeight)
    {
        int cx = screenWidth / 2, cy = screenHeight / 2;
        var crosshair = new Color(255, 255, 255, 190);

        // Vitals sit just above the hotbar, left-aligned with it.
        int hotbarWidth = Inventory.HotbarSize * SlotRenderer.SlotSize + (Inventory.HotbarSize - 1) * 4;
        int x0 = (screenWidth - hotbarWidth) / 2;
        int heartsY = screenHeight - SlotRenderer.SlotSize - 12 - _heart.Height * SpriteScale - 6;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        spriteBatch.Draw(_pixel, new Rectangle(cx - 8, cy - 1, 16, 2), crosshair);
        spriteBatch.Draw(_pixel, new Rectangle(cx - 1, cy - 8, 2, 16), crosshair);

        int step = _heart.Width * SpriteScale + 2;
        for (int i = 0; i < PlayerHealth.MaxHealth / 2; i++)
        {
            var pos = new Rectangle(x0 + i * step, heartsY, _heart.Width * SpriteScale, _heart.Height * SpriteScale);
            spriteBatch.Draw(_heart, pos, new Color(40, 20, 20, 200)); // empty backdrop
            int halves = health.Health - i * 2;
            if (halves >= 2)
            {
                spriteBatch.Draw(_heart, pos, new Color(220, 40, 40));
            }
            else if (halves == 1)
            {
                int halfW = _heart.Width / 2 + 1;
                var src = new Rectangle(0, 0, halfW, _heart.Height);
                var dst = new Rectangle(pos.X, pos.Y, halfW * SpriteScale, pos.Height);
                spriteBatch.Draw(_heart, dst, src, new Color(220, 40, 40));
            }
        }

        if (health.IsUnderwater)
        {
            int bubbleY = heartsY - _bubble.Height * SpriteScale - 4;
            int bubbles = (int)System.MathF.Ceiling(health.Air);
            for (int i = 0; i < bubbles; i++)
                spriteBatch.Draw(_bubble,
                    new Rectangle(x0 + i * (_bubble.Width * SpriteScale + 2), bubbleY,
                        _bubble.Width * SpriteScale, _bubble.Height * SpriteScale),
                    new Color(190, 220, 255));
        }

        spriteBatch.End();
    }
}
