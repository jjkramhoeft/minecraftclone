using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MinecraftClone.UI;

/// <summary>
/// The main menu (world slots + exit) and the Esc pause menu (resume / save /
/// quit to menu), drawn with the pixel font over whatever is behind them.
/// Layout is recomputed from the screen size in both Update and Draw, so
/// hit-testing and rendering can never disagree.
/// </summary>
public class MenuScreen
{
    public enum Mode { Hidden, Main, Pause }

    public const int WorldSlots = 3;

    public Mode Current { get; set; } = Mode.Hidden;

    /// <summary>Which world slots have a save on disk — refreshed by the
    /// composition root whenever the main menu opens.</summary>
    public bool[] SlotSaved { get; } = new bool[WorldSlots];

    public event Action<int> WorldChosen;
    public event Action<int> WorldDeleted;
    public event Action ResumeRequested;
    public event Action SaveRequested;
    public event Action QuitToMenuRequested;
    public event Action ExitRequested;

    private const int ButtonWidth = 340;
    private const int ButtonHeight = 44;
    private const int ButtonGap = 12;
    private const int DeleteWidth = 44;

    private readonly Texture2D _pixel;

    public MenuScreen(GraphicsDevice device)
    {
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Update(MouseState mouse, MouseState previousMouse, int screenWidth, int screenHeight)
    {
        bool click = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        if (!click || Current == Mode.Hidden)
            return;

        if (Current == Mode.Main)
        {
            for (int i = 0; i < WorldSlots; i++)
            {
                if (SlotRect(i, screenWidth, screenHeight).Contains(mouse.Position))
                {
                    WorldChosen?.Invoke(i);
                    return;
                }
                if (SlotSaved[i] && DeleteRect(i, screenWidth, screenHeight).Contains(mouse.Position))
                {
                    WorldDeleted?.Invoke(i);
                    return;
                }
            }
            if (ExitRect(screenWidth, screenHeight).Contains(mouse.Position))
                ExitRequested?.Invoke();
        }
        else // Pause
        {
            if (PauseRect(0, screenWidth, screenHeight).Contains(mouse.Position))
                ResumeRequested?.Invoke();
            else if (PauseRect(1, screenWidth, screenHeight).Contains(mouse.Position))
                SaveRequested?.Invoke();
            else if (PauseRect(2, screenWidth, screenHeight).Contains(mouse.Position))
                QuitToMenuRequested?.Invoke();
        }
    }

    public void Draw(SpriteBatch spriteBatch, PixelFont font, MouseState mouse, int screenWidth, int screenHeight)
    {
        if (Current == Mode.Hidden)
            return;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight),
            new Color(0, 0, 0, Current == Mode.Main ? 90 : 150));

        DrawCenteredText(spriteBatch, font, "MINECRAFT CLONE", screenWidth / 2, screenHeight / 5, 6, Color.White);

        if (Current == Mode.Main)
        {
            for (int i = 0; i < WorldSlots; i++)
            {
                var rect = SlotRect(i, screenWidth, screenHeight);
                DrawButton(spriteBatch, font, rect, $"WORLD {i + 1}", mouse);
                string status = SlotSaved[i] ? "SAVED" : "NEW";
                font.Draw(spriteBatch, status, rect.Right - PixelFont.MeasureWidth(status, 2) - 10,
                    rect.Y + (rect.Height - PixelFont.GlyphHeight * 2) / 2, 2, new Color(160, 160, 160));

                if (SlotSaved[i])
                    DrawButton(spriteBatch, font, DeleteRect(i, screenWidth, screenHeight), "X", mouse,
                        new Color(120, 40, 40, 220));
            }
            DrawButton(spriteBatch, font, ExitRect(screenWidth, screenHeight), "EXIT", mouse);
        }
        else
        {
            DrawButton(spriteBatch, font, PauseRect(0, screenWidth, screenHeight), "RESUME", mouse);
            DrawButton(spriteBatch, font, PauseRect(1, screenWidth, screenHeight), "SAVE", mouse);
            DrawButton(spriteBatch, font, PauseRect(2, screenWidth, screenHeight), "QUIT TO MENU", mouse);
        }

        spriteBatch.End();
    }

    private void DrawButton(SpriteBatch spriteBatch, PixelFont font, Rectangle rect, string label,
        MouseState mouse, Color? background = null)
    {
        bool hover = rect.Contains(mouse.Position);
        var back = background ?? new Color(40, 40, 40, 220);
        if (hover)
            back = new Color(Math.Min(back.R + 30, 255), Math.Min(back.G + 30, 255), Math.Min(back.B + 30, 255), back.A);
        spriteBatch.Draw(_pixel, rect, back);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Color(90, 90, 90));
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Color(15, 15, 15));

        const int scale = 3;
        font.Draw(spriteBatch, label,
            rect.X + 12, rect.Y + (rect.Height - PixelFont.GlyphHeight * scale) / 2, scale,
            hover ? Color.White : new Color(220, 220, 220));
    }

    private static void DrawCenteredText(SpriteBatch spriteBatch, PixelFont font, string text, int centerX, int y, int scale, Color color)
        => font.Draw(spriteBatch, text, centerX - PixelFont.MeasureWidth(text, scale) / 2, y, scale, color);

    private static Rectangle SlotRect(int index, int screenWidth, int screenHeight) =>
        new((screenWidth - ButtonWidth) / 2,
            screenHeight * 2 / 5 + index * (ButtonHeight + ButtonGap),
            ButtonWidth, ButtonHeight);

    private static Rectangle DeleteRect(int index, int screenWidth, int screenHeight)
    {
        var slot = SlotRect(index, screenWidth, screenHeight);
        return new Rectangle(slot.Right + 8, slot.Y, DeleteWidth, ButtonHeight);
    }

    private static Rectangle ExitRect(int screenWidth, int screenHeight) =>
        new((screenWidth - ButtonWidth) / 2,
            screenHeight * 2 / 5 + WorldSlots * (ButtonHeight + ButtonGap) + 24,
            ButtonWidth, ButtonHeight);

    private static Rectangle PauseRect(int index, int screenWidth, int screenHeight) =>
        new((screenWidth - ButtonWidth) / 2,
            screenHeight * 2 / 5 + index * (ButtonHeight + ButtonGap),
            ButtonWidth, ButtonHeight);
}
