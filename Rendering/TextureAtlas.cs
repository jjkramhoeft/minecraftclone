using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

/// <summary>
/// The block texture atlas, generated procedurally at startup: a 16x16 grid of
/// 16x16-pixel tiles (256x256 total), each a base color with deterministic
/// per-pixel brightness jitter — the classic voxel-game speckle. No image
/// assets, no content pipeline. Swapping in a real atlas.png later only means
/// replacing this generation with Texture2D.FromStream.
/// </summary>
public class TextureAtlas
{
    public const int TileSize = 16;
    public const int TilesPerRow = 16;
    public const int AtlasSize = TileSize * TilesPerRow;

    public Texture2D Texture { get; }

    public TextureAtlas(GraphicsDevice device)
    {
        var pixels = new Color[AtlasSize * AtlasSize];

        DrawSpeckled(pixels, BlockInfo.TileGrassTop, new Color(96, 176, 64), 14);
        DrawGrassSide(pixels);
        DrawSpeckled(pixels, BlockInfo.TileDirt, new Color(134, 96, 67), 12);
        DrawSpeckled(pixels, BlockInfo.TileStone, new Color(125, 125, 125), 9);
        DrawSpeckled(pixels, BlockInfo.TileSand, new Color(219, 207, 163), 9);
        DrawBark(pixels);
        DrawWoodTop(pixels);
        DrawLeaves(pixels);
        DrawSpeckled(pixels, BlockInfo.TileWater, new Color(58, 110, 216), 10);
        DrawPlanks(pixels);
        DrawBricks(pixels);
        DrawStick(pixels);
        DrawTool(pixels, BlockInfo.TileWoodenPickaxe, WoodHeadColor, ToolShape.Pickaxe);
        DrawTool(pixels, BlockInfo.TileStonePickaxe, StoneHeadColor, ToolShape.Pickaxe);
        DrawTool(pixels, BlockInfo.TileWoodenAxe, WoodHeadColor, ToolShape.Axe);
        DrawTool(pixels, BlockInfo.TileStoneAxe, StoneHeadColor, ToolShape.Axe);
        DrawTool(pixels, BlockInfo.TileWoodenShovel, WoodHeadColor, ToolShape.Shovel);
        DrawTool(pixels, BlockInfo.TileStoneShovel, StoneHeadColor, ToolShape.Shovel);
        DrawCracks(pixels, BlockInfo.TileCrack0, 3);
        DrawCracks(pixels, BlockInfo.TileCrack1, 6);
        DrawCracks(pixels, BlockInfo.TileCrack2, 10);
        DrawCracks(pixels, BlockInfo.TileCrack3, 16);
        DrawFlower(pixels, BlockInfo.TileFlowerRed, new Color(214, 48, 44));
        DrawFlower(pixels, BlockInfo.TileFlowerYellow, new Color(238, 210, 60));
        DrawFlower(pixels, BlockInfo.TileFlowerPoppy, new Color(238, 238, 230));
        DrawReeds(pixels);
        DrawSpeckled(pixels, BlockInfo.TileSkin, new Color(224, 180, 145), 5);
        DrawSpeckled(pixels, BlockInfo.TileShirt, new Color(66, 150, 178), 7);
        DrawSpeckled(pixels, BlockInfo.TilePants, new Color(58, 68, 118), 6);
        DrawFace(pixels);
        DrawDisc(pixels, BlockInfo.TileSun, new Color(255, 236, 160), new Color(255, 196, 90));
        DrawDisc(pixels, BlockInfo.TileMoon, new Color(226, 228, 240), new Color(180, 184, 205));

        Texture = new Texture2D(device, AtlasSize, AtlasSize);
        Texture.SetData(pixels);
    }

    /// <summary>
    /// UV rectangle of a tile as (uMin, vMin, uMax, vMax), inset by half a texel
    /// on every side so point sampling never bleeds into the neighboring tile.
    /// </summary>
    public static Vector4 GetUVBounds(int tile)
    {
        const float texel = 1f / AtlasSize;
        float u0 = (tile % TilesPerRow) * TileSize * texel + texel * 0.5f;
        float v0 = (tile / TilesPerRow) * TileSize * texel + texel * 0.5f;
        return new Vector4(u0, v0, u0 + (TileSize - 1) * texel, v0 + (TileSize - 1) * texel);
    }

    private static void SetPixel(Color[] pixels, int tile, int x, int y, Color color)
    {
        int px = (tile % TilesPerRow) * TileSize + x;
        int py = (tile / TilesPerRow) * TileSize + y;
        pixels[px + py * AtlasSize] = color;
    }

    private static Color Jitter(Random rng, Color baseColor, int amount)
    {
        int j = rng.Next(-amount, amount + 1);
        return new Color(
            Math.Clamp(baseColor.R + j, 0, 255),
            Math.Clamp(baseColor.G + j, 0, 255),
            Math.Clamp(baseColor.B + j, 0, 255));
    }

    private static Random RngFor(int tile) => new(tile * 7919 + 12345);

    private static void DrawSpeckled(Color[] pixels, int tile, Color baseColor, int jitter)
    {
        var rng = RngFor(tile);
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, tile, x, y, Jitter(rng, baseColor, jitter));
    }

    private static void DrawGrassSide(Color[] pixels)
    {
        var rng = RngFor(BlockInfo.TileGrassSide);
        var dirt = new Color(134, 96, 67);
        var grass = new Color(96, 176, 64);
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                // Grass drapes over the top edge with a ragged boundary.
                bool isGrass = y < 2 || (y == 2 && rng.Next(2) == 0) || (y == 3 && rng.Next(4) == 0);
                SetPixel(pixels, BlockInfo.TileGrassSide, x, y, Jitter(rng, isGrass ? grass : dirt, 12));
            }
        }
    }

    private static void DrawBark(Color[] pixels)
    {
        var rng = RngFor(BlockInfo.TileWoodSide);
        var baseColor = new Color(102, 81, 50);
        for (int x = 0; x < TileSize; x++)
        {
            int columnShade = rng.Next(-18, 19); // vertical streaks
            for (int y = 0; y < TileSize; y++)
            {
                var c = new Color(
                    Math.Clamp(baseColor.R + columnShade, 0, 255),
                    Math.Clamp(baseColor.G + columnShade, 0, 255),
                    Math.Clamp(baseColor.B + columnShade, 0, 255));
                SetPixel(pixels, BlockInfo.TileWoodSide, x, y, Jitter(rng, c, 6));
            }
        }
    }

    private static void DrawWoodTop(Color[] pixels)
    {
        var rng = RngFor(BlockInfo.TileWoodTop);
        var light = new Color(168, 132, 84);
        var dark = new Color(126, 96, 58);
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                // Square growth rings around the center.
                int ring = Math.Max(Math.Abs(x * 2 - 15), Math.Abs(y * 2 - 15)) / 2;
                SetPixel(pixels, BlockInfo.TileWoodTop, x, y, Jitter(rng, ring % 2 == 0 ? light : dark, 7));
            }
        }
    }

    private static void DrawPlanks(Color[] pixels)
    {
        var rng = RngFor(BlockInfo.TilePlanks);
        var baseColor = new Color(168, 132, 84);
        var seam = new Color(110, 84, 50);
        for (int y = 0; y < TileSize; y++)
        {
            bool seamRow = y % 4 == 3;
            // Vertical plank-end joints, staggered per row band.
            int jointX = (y / 4 * 7 + 3) % TileSize;
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, BlockInfo.TilePlanks, x, y,
                    seamRow || x == jointX ? seam : Jitter(rng, baseColor, 8));
        }
    }

    private static void DrawBricks(Color[] pixels)
    {
        var rng = RngFor(BlockInfo.TileBricks);
        var brick = new Color(146, 60, 48);
        var mortar = new Color(180, 172, 162);
        for (int y = 0; y < TileSize; y++)
        {
            bool mortarRow = y % 4 == 3;
            int offset = y / 4 % 2 == 0 ? 0 : 4; // running bond stagger
            for (int x = 0; x < TileSize; x++)
            {
                bool mortarColumn = (x + offset) % 8 == 7;
                SetPixel(pixels, BlockInfo.TileBricks, x, y,
                    mortarRow || mortarColumn ? mortar : Jitter(rng, brick, 10));
            }
        }
    }

    private static readonly Color HandleColor = new(102, 81, 50);
    private static readonly Color WoodHeadColor = new(168, 132, 84);
    private static readonly Color StoneHeadColor = new(125, 125, 125);

    private enum ToolShape { Pickaxe, Axe, Shovel }

    /// <summary>UI-only icons: a diagonal handle with a head shape at the top
    /// end, on a transparent background.</summary>
    private static void DrawTool(Color[] pixels, int tile, Color head, ToolShape shape)
    {
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, tile, x, y, Color.Transparent);

        // Handle from bottom-left to upper-right, 2 px thick.
        for (int i = 0; i < 10; i++)
        {
            SetPixel(pixels, tile, 3 + i, 13 - i, HandleColor);
            SetPixel(pixels, tile, 4 + i, 13 - i, HandleColor);
        }

        switch (shape)
        {
            case ToolShape.Pickaxe:
                // Arced head across the top, drooping at both ends.
                for (int x = 4; x <= 12; x++)
                {
                    SetPixel(pixels, tile, x, 2, head);
                    SetPixel(pixels, tile, x, 3, head);
                }
                for (int d = 0; d < 3; d++)
                {
                    SetPixel(pixels, tile, 3, 3 + d, head);
                    SetPixel(pixels, tile, 13, 3 + d, head);
                }
                break;
            case ToolShape.Axe:
                // Blade hanging off the left side of the handle's top end.
                for (int y = 1; y <= 6; y++)
                    for (int x = 7; x <= 11; x++)
                        if (x + y <= 15)
                            SetPixel(pixels, tile, x, y, head);
                break;
            case ToolShape.Shovel:
                // Rounded blade capping the handle's top end.
                for (int y = 0; y <= 4; y++)
                    for (int x = 10; x <= 14; x++)
                        if (!((x == 10 || x == 14) && (y == 0 || y == 4)))
                            SetPixel(pixels, tile, x, y, head);
                break;
        }
    }

    /// <summary>Shore reeds: a few full-height stalks with leaf nubs, on a
    /// transparent background (binary alpha for the cutout pass).</summary>
    private static void DrawReeds(Color[] pixels)
    {
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, BlockInfo.TileReeds, x, y, Color.Transparent);

        var rng = RngFor(BlockInfo.TileReeds);
        var stalk = new Color(132, 168, 78);
        var tip = new Color(96, 132, 58);

        Span<int> stalkColumns = stackalloc int[] { 3, 7, 11, 14 };
        foreach (int column in stalkColumns)
        {
            int x = column;
            for (int y = TileSize - 1; y >= 0; y--)
            {
                SetPixel(pixels, BlockInfo.TileReeds, x, y, Jitter(rng, y < 5 ? tip : stalk, 8));
                if (y % 5 == 0 && rng.Next(2) == 0)
                    x = Math.Clamp(x + rng.Next(-1, 2), 0, TileSize - 1); // gentle kink
                if (y is 6 or 10 && rng.Next(2) == 0)
                    SetPixel(pixels, BlockInfo.TileReeds, Math.Clamp(x + 1, 0, TileSize - 1), y, Jitter(rng, stalk, 8)); // leaf nub
            }
        }
    }

    /// <summary>Sun/moon sprite: a soft-edged disc on a transparent background,
    /// blending from a bright core to a rim color; the moon gets crater speckle.</summary>
    private static void DrawDisc(Color[] pixels, int tile, Color core, Color rim)
    {
        var rng = RngFor(tile);
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                float distance = MathF.Sqrt((x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f));
                if (distance > 6.5f)
                {
                    SetPixel(pixels, tile, x, y, Color.Transparent);
                    continue;
                }

                var color = Color.Lerp(core, rim, Math.Clamp(distance / 6.5f, 0f, 1f));
                if (tile == BlockInfo.TileMoon && rng.Next(7) == 0)
                    color = Color.Lerp(color, new Color(140, 145, 170), 0.6f); // craters
                if (distance > 5.5f)
                    color *= 1f - (distance - 5.5f); // soft edge fade to transparent
                SetPixel(pixels, tile, x, y, color);
            }
        }
    }

    /// <summary>The head's front tile: skin with eyes and a hint of a mouth.</summary>
    private static void DrawFace(Color[] pixels)
    {
        var rng = RngFor(BlockInfo.TileFace);
        var skin = new Color(224, 180, 145);
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, BlockInfo.TileFace, x, y, Jitter(rng, skin, 5));

        var eye = new Color(48, 40, 82);
        SetPixel(pixels, BlockInfo.TileFace, 4, 6, eye);
        SetPixel(pixels, BlockInfo.TileFace, 5, 6, eye);
        SetPixel(pixels, BlockInfo.TileFace, 10, 6, eye);
        SetPixel(pixels, BlockInfo.TileFace, 11, 6, eye);
        var mouth = new Color(178, 128, 100);
        SetPixel(pixels, BlockInfo.TileFace, 7, 11, mouth);
        SetPixel(pixels, BlockInfo.TileFace, 8, 11, mouth);
    }

    /// <summary>Flower sprite for the cross-quad cutout mesh: transparent
    /// background (binary alpha, so alpha-testing cuts cleanly), a green stem
    /// with two leaves and a petal cluster on top.</summary>
    private static void DrawFlower(Color[] pixels, int tile, Color petal)
    {
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, tile, x, y, Color.Transparent);

        var stem = new Color(58, 128, 44);
        for (int y = 5; y < TileSize; y++)
            SetPixel(pixels, tile, 7, y, stem);
        SetPixel(pixels, tile, 6, 10, stem); // leaves
        SetPixel(pixels, tile, 5, 9, stem);
        SetPixel(pixels, tile, 8, 12, stem);
        SetPixel(pixels, tile, 9, 11, stem);

        var center = new Color(90, 70, 30);
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
                if (Math.Abs(dx) + Math.Abs(dy) <= 2)
                    SetPixel(pixels, tile, 7 + dx, 3 + dy, petal);
        SetPixel(pixels, tile, 7, 3, center);
    }

    /// <summary>Breaking-progress overlay: dark crack strands random-walking out
    /// from the tile center, denser per stage, on a transparent background.</summary>
    private static void DrawCracks(Color[] pixels, int tile, int strands)
    {
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, tile, x, y, Color.Transparent);

        var rng = RngFor(tile);
        var crack = new Color(22, 18, 14, 210);
        for (int s = 0; s < strands; s++)
        {
            int x = 6 + rng.Next(5);
            int y = 6 + rng.Next(5);
            int length = 4 + rng.Next(6);
            for (int i = 0; i < length; i++)
            {
                SetPixel(pixels, tile, Math.Clamp(x, 0, TileSize - 1), Math.Clamp(y, 0, TileSize - 1), crack);
                x += rng.Next(-1, 2);
                y += rng.Next(-1, 2);
            }
        }
    }

    private static void DrawStick(Color[] pixels)
    {
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, BlockInfo.TileStick, x, y, Color.Transparent);
        for (int i = 0; i < 10; i++)
        {
            SetPixel(pixels, BlockInfo.TileStick, 3 + i, 12 - i, HandleColor);
            SetPixel(pixels, BlockInfo.TileStick, 4 + i, 12 - i, HandleColor);
        }
    }

    private static void DrawLeaves(Color[] pixels)
    {
        var rng = RngFor(BlockInfo.TileLeaves);
        var baseColor = new Color(58, 138, 42);
        var shadow = new Color(30, 84, 22);
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, BlockInfo.TileLeaves, x, y,
                    rng.Next(10) == 0 ? shadow : Jitter(rng, baseColor, 20));
    }
}
