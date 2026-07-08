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
        DrawBark(pixels, BlockInfo.TileWoodSide, new Color(102, 81, 50));
        DrawLogTop(pixels, BlockInfo.TileWoodTop, new Color(168, 132, 84), new Color(126, 96, 58));
        DrawLeavesTile(pixels, BlockInfo.TileLeaves, new Color(58, 138, 42), new Color(30, 84, 22));
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
        DrawOre(pixels, BlockInfo.TileCoalOre, new Color(38, 38, 40));
        DrawOre(pixels, BlockInfo.TileIronOre, new Color(216, 168, 128));
        DrawTool(pixels, BlockInfo.TileIronPickaxe, IronHeadColor, ToolShape.Pickaxe);
        DrawTool(pixels, BlockInfo.TileIronAxe, IronHeadColor, ToolShape.Axe);
        DrawTool(pixels, BlockInfo.TileIronShovel, IronHeadColor, ToolShape.Shovel);
        DrawLump(pixels, BlockInfo.TileCoal, new Color(38, 38, 40));
        DrawIngot(pixels, BlockInfo.TileIronIngot, IronHeadColor);
        DrawTorch(pixels);
        DrawSpeckled(pixels, BlockInfo.TilePig, new Color(232, 160, 150), 8);
        DrawSpeckled(pixels, BlockInfo.TileChicken, new Color(238, 236, 230), 6);
        DrawSpeckled(pixels, BlockInfo.TileFurnaceSide, new Color(110, 110, 110), 9);
        DrawFurnaceFront(pixels, BlockInfo.TileFurnaceFront, lit: false);
        DrawFurnaceFront(pixels, BlockInfo.TileFurnaceFrontLit, lit: true);
        DrawGlass(pixels);
        DrawChest(pixels, BlockInfo.TileChestSide, latch: false);
        DrawChest(pixels, BlockInfo.TileChestFront, latch: true);
        DrawBucket(pixels, BlockInfo.TileBucket, filled: false);
        DrawBucket(pixels, BlockInfo.TileWaterBucket, filled: true);
        DrawCraftingTable(pixels, BlockInfo.TileCraftingTop, top: true);
        DrawCraftingTable(pixels, BlockInfo.TileCraftingSide, top: false);
        DrawBirchBark(pixels);
        DrawLogTop(pixels, BlockInfo.TileBirchTop, new Color(228, 226, 214), new Color(198, 196, 184));
        DrawLeavesTile(pixels, BlockInfo.TileBirchLeaves, new Color(112, 176, 86), new Color(72, 132, 54));
        DrawBark(pixels, BlockInfo.TilePineBark, new Color(84, 58, 38));
        DrawLogTop(pixels, BlockInfo.TilePineTop, new Color(120, 88, 58), new Color(92, 66, 42));
        DrawLeavesTile(pixels, BlockInfo.TilePineLeaves, new Color(40, 96, 48), new Color(24, 66, 32));
        DrawFern(pixels);
        DrawCobblestone(pixels);

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

    /// <summary>Bark side: vertical streaks over a base color. Shared by every
    /// log species — the caller picks the tile and base color.</summary>
    private static void DrawBark(Color[] pixels, int tile, Color baseColor)
    {
        var rng = RngFor(tile);
        for (int x = 0; x < TileSize; x++)
        {
            int columnShade = rng.Next(-18, 19); // vertical streaks
            for (int y = 0; y < TileSize; y++)
            {
                var c = new Color(
                    Math.Clamp(baseColor.R + columnShade, 0, 255),
                    Math.Clamp(baseColor.G + columnShade, 0, 255),
                    Math.Clamp(baseColor.B + columnShade, 0, 255));
                SetPixel(pixels, tile, x, y, Jitter(rng, c, 6));
            }
        }
    }

    /// <summary>Birch bark: pale trunk with scattered dark horizontal lenticels.</summary>
    private static void DrawBirchBark(Color[] pixels)
    {
        var rng = RngFor(BlockInfo.TileBirchBark);
        var pale = new Color(224, 224, 214);
        var dark = new Color(60, 62, 58);
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, BlockInfo.TileBirchBark, x, y, Jitter(rng, pale, 7));

        for (int d = 0; d < 6; d++)
        {
            int y = rng.Next(TileSize);
            int x = rng.Next(TileSize - 3);
            int len = 2 + rng.Next(3);
            for (int i = 0; i < len; i++)
                SetPixel(pixels, BlockInfo.TileBirchBark, x + i, y, Jitter(rng, dark, 8));
        }
    }

    /// <summary>Log end grain: square growth rings around the center, in the
    /// caller's light/dark ring colors.</summary>
    private static void DrawLogTop(Color[] pixels, int tile, Color light, Color dark)
    {
        var rng = RngFor(tile);
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int ring = Math.Max(Math.Abs(x * 2 - 15), Math.Abs(y * 2 - 15)) / 2;
                SetPixel(pixels, tile, x, y, Jitter(rng, ring % 2 == 0 ? light : dark, 7));
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

    /// <summary>Crafting table: a planks base, with a 3x3 grid on the top face
    /// and a framed tool-cubby motif on the sides.</summary>
    private static void DrawCraftingTable(Color[] pixels, int tile, bool top)
    {
        var rng = RngFor(tile);
        var baseColor = new Color(168, 132, 84);
        var seam = new Color(110, 84, 50);
        var dark = new Color(84, 62, 36);

        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, tile, x, y, Jitter(rng, baseColor, 7));

        if (top)
        {
            // Grid lines at the thirds carve a 3x3 crafting surface.
            for (int i = 0; i < TileSize; i++)
            {
                SetPixel(pixels, tile, i, 5, seam);
                SetPixel(pixels, tile, i, 10, seam);
                SetPixel(pixels, tile, 5, i, seam);
                SetPixel(pixels, tile, 10, i, seam);
                SetPixel(pixels, tile, i, 0, dark);
                SetPixel(pixels, tile, i, TileSize - 1, dark);
                SetPixel(pixels, tile, 0, i, dark);
                SetPixel(pixels, tile, TileSize - 1, i, dark);
            }
        }
        else
        {
            // Plank seams plus a small square "cubby" of tools, bottom-left.
            for (int y = 0; y < TileSize; y++)
                if (y % 4 == 3)
                    for (int x = 0; x < TileSize; x++)
                        SetPixel(pixels, tile, x, y, seam);
            for (int y = 8; y <= 13; y++)
                for (int x = 2; x <= 7; x++)
                    SetPixel(pixels, tile, x, y,
                        x == 2 || x == 7 || y == 8 || y == 13 ? dark : Jitter(rng, seam, 6));
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
    private static readonly Color IronHeadColor = new(216, 216, 224);

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

    /// <summary>Fern for the cross-quad cutout mesh: a fan of arching fronds
    /// with leaflet nubs, on a transparent background (binary alpha).</summary>
    private static void DrawFern(Color[] pixels)
    {
        int tile = BlockInfo.TileFern;
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, tile, x, y, Color.Transparent);

        var rng = RngFor(tile);
        var frond = new Color(74, 128, 52);
        var tip = new Color(108, 158, 78);

        // Each frond rises from the base tuft (x=8, y=15) and leans out to its
        // tip offset, with short leaflets along the spine.
        Span<int> spread = stackalloc int[] { -4, -2, 0, 2, 4 };
        const int steps = 11;
        foreach (int dxTip in spread)
        {
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)(steps - 1);
                int x = 8 + (int)MathF.Round(dxTip * t);
                int y = 15 - i;
                if (x < 0 || x >= TileSize || y < 0)
                    continue;
                SetPixel(pixels, tile, x, y, Jitter(rng, i >= steps - 3 ? tip : frond, 8));
                if (i % 2 == 0)
                {
                    int lx = x + (dxTip < 0 ? -1 : dxTip > 0 ? 1 : (i % 4 == 0 ? 1 : -1));
                    if (lx >= 0 && lx < TileSize)
                        SetPixel(pixels, tile, lx, y, Jitter(rng, frond, 8));
                }
            }
        }
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

    /// <summary>Cobblestone: rounded gray cobbles of varying shade over dark
    /// mortar, with edge-straddling stones so the tile reads seamless.</summary>
    private static void DrawCobblestone(Color[] pixels)
    {
        int tile = BlockInfo.TileCobblestone;
        var rng = RngFor(tile);
        var mortar = new Color(68, 68, 70);
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, tile, x, y, Jitter(rng, mortar, 5));

        Span<int> cxs = stackalloc int[] { 3, 10, 4, 12, 8, 0, 15 };
        Span<int> cys = stackalloc int[] { 3, 4, 11, 12, 8, 10, 1 };
        for (int k = 0; k < cxs.Length; k++)
        {
            int r = 3 + rng.Next(2); // 3-4
            int g = 118 + rng.Next(28);
            var stone = new Color(g, g, Math.Min(255, g + 3));
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (dx * dx + dy * dy <= r * r)
                    {
                        int px = cxs[k] + dx, py = cys[k] + dy;
                        if (px >= 0 && px < TileSize && py >= 0 && py < TileSize)
                            SetPixel(pixels, tile, px, py, Jitter(rng, stone, 8));
                    }
        }
    }

    /// <summary>Ore block face: stone speckle with a few 2x2-ish mineral spots.</summary>
    private static void DrawOre(Color[] pixels, int tile, Color mineral)
    {
        var rng = RngFor(tile);
        var stone = new Color(125, 125, 125);
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, tile, x, y, Jitter(rng, stone, 9));

        for (int spot = 0; spot < 6; spot++)
        {
            int sx = rng.Next(1, TileSize - 2);
            int sy = rng.Next(1, TileSize - 2);
            SetPixel(pixels, tile, sx, sy, Jitter(rng, mineral, 12));
            SetPixel(pixels, tile, sx + 1, sy, Jitter(rng, mineral, 12));
            SetPixel(pixels, tile, sx, sy + 1, Jitter(rng, mineral, 12));
            if (rng.Next(2) == 0)
                SetPixel(pixels, tile, sx + 1, sy + 1, Jitter(rng, mineral, 12));
        }
    }

    /// <summary>Item icon: an irregular rounded lump on a transparent background.</summary>
    private static void DrawLump(Color[] pixels, int tile, Color color)
    {
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, tile, x, y, Color.Transparent);

        var rng = RngFor(tile);
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                float dist = MathF.Sqrt((x - 7.5f) * (x - 7.5f) + (y - 8f) * (y - 8f));
                if (dist < 4.5f + rng.Next(2))
                    SetPixel(pixels, tile, x, y, Jitter(rng, color, 14));
            }
        }
    }

    /// <summary>Item icon: a metal bar with a highlighted top edge.</summary>
    /// <summary>Bucket icon: a tapered tin body with a handle arc; the filled
    /// variant shows water at the brim.</summary>
    private static void DrawBucket(Color[] pixels, int tile, bool filled)
    {
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, tile, x, y, Color.Transparent);

        var metal = new Color(150, 150, 158);
        var edge = new Color(100, 100, 110);
        for (int y = 6; y <= 13; y++)
        {
            int inset = 3 + (y - 6) / 4;
            for (int x = inset; x <= 15 - inset; x++)
            {
                bool rim = x == inset || x == 15 - inset || y == 13 || y == 6;
                SetPixel(pixels, tile, x, y, rim ? edge : metal);
            }
        }
        if (filled)
        {
            var water = new Color(58, 110, 216);
            for (int y = 7; y <= 8; y++)
                for (int x = 4; x <= 11; x++)
                    SetPixel(pixels, tile, x, y, water);
        }
        // Handle: shallow arc over the brim.
        SetPixel(pixels, tile, 3, 5, edge);
        SetPixel(pixels, tile, 4, 4, edge);
        SetPixel(pixels, tile, 5, 3, edge);
        for (int x = 6; x <= 9; x++)
            SetPixel(pixels, tile, x, 2, edge);
        SetPixel(pixels, tile, 10, 3, edge);
        SetPixel(pixels, tile, 11, 4, edge);
        SetPixel(pixels, tile, 12, 5, edge);
    }

    private static void DrawIngot(Color[] pixels, int tile, Color metal)
    {
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, tile, x, y, Color.Transparent);

        var rng = RngFor(tile);
        var shadow = new Color(metal.R * 3 / 4, metal.G * 3 / 4, metal.B * 3 / 4);
        for (int y = 6; y <= 11; y++)
            for (int x = 2; x <= 13; x++)
                SetPixel(pixels, tile, x, y, Jitter(rng, y >= 10 ? shadow : metal, 6));
        for (int x = 3; x <= 12; x++)
            SetPixel(pixels, tile, x, 5, Color.White * 0.9f); // top-edge glint
    }

    /// <summary>Furnace face: stone with a dark mouth near the bottom; the lit
    /// variant fills the mouth with fire colors.</summary>
    private static void DrawFurnaceFront(Color[] pixels, int tile, bool lit)
    {
        var rng = RngFor(tile);
        var stone = new Color(110, 110, 110);
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, tile, x, y, Jitter(rng, stone, 9));

        var mouthDark = new Color(24, 20, 18);
        var fire = new Color(240, 150, 40);
        var fireCore = new Color(255, 220, 110);
        for (int y = 8; y <= 13; y++)
        {
            for (int x = 4; x <= 11; x++)
            {
                Color c = mouthDark;
                if (lit)
                    c = (x + y) % 3 == 0 ? fireCore : fire;
                SetPixel(pixels, tile, x, y, Jitter(rng, c, lit ? 15 : 4));
            }
        }
    }

    /// <summary>Glass: transparent interior with a pale frame and a couple of
    /// diagonal glints — meshed in the cutout (alpha-tested) pass.</summary>
    private static void DrawGlass(Color[] pixels)
    {
        var frame = new Color(210, 230, 235);
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                bool edge = x == 0 || y == 0 || x == TileSize - 1 || y == TileSize - 1;
                bool glint = x - y is 4 or 5 && x < 11;
                SetPixel(pixels, BlockInfo.TileGlass, x, y,
                    edge || glint ? frame : Color.Transparent);
            }
        }
    }

    /// <summary>Chest: warm planks with a dark rim and lid seam; the front
    /// adds the latch.</summary>
    private static void DrawChest(Color[] pixels, int tile, bool latch)
    {
        var rng = RngFor(tile);
        var wood = new Color(158, 116, 66);
        var rim = new Color(92, 66, 38);
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                bool edge = x == 0 || y == 0 || x == TileSize - 1 || y == TileSize - 1;
                bool seam = y == 5; // lid line
                SetPixel(pixels, tile, x, y, edge || seam ? rim : Jitter(rng, wood, 9));
            }
        }
        if (latch)
        {
            var metal = new Color(190, 190, 200);
            for (int y = 4; y <= 7; y++)
            {
                SetPixel(pixels, tile, 7, y, metal);
                SetPixel(pixels, tile, 8, y, metal);
            }
        }
    }

    /// <summary>Torch for the cross-quad cutout mesh: a short stick with a
    /// glowing head, on a transparent background.</summary>
    private static void DrawTorch(Color[] pixels)
    {
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, BlockInfo.TileTorch, x, y, Color.Transparent);

        var rng = RngFor(BlockInfo.TileTorch);
        for (int y = 6; y < TileSize; y++)
        {
            SetPixel(pixels, BlockInfo.TileTorch, 7, y, Jitter(rng, HandleColor, 8));
            SetPixel(pixels, BlockInfo.TileTorch, 8, y, Jitter(rng, HandleColor, 8));
        }

        var flameCore = new Color(255, 232, 130);
        var flameEdge = new Color(240, 140, 40);
        SetPixel(pixels, BlockInfo.TileTorch, 7, 2, flameCore);
        SetPixel(pixels, BlockInfo.TileTorch, 8, 2, flameCore);
        for (int y = 3; y <= 5; y++)
        {
            SetPixel(pixels, BlockInfo.TileTorch, 6, y, flameEdge);
            SetPixel(pixels, BlockInfo.TileTorch, 7, y, flameCore);
            SetPixel(pixels, BlockInfo.TileTorch, 8, y, flameCore);
            SetPixel(pixels, BlockInfo.TileTorch, 9, y, flameEdge);
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

    /// <summary>Leafy speckle in the caller's base/shadow greens — shared by
    /// oak, birch, and pine (needles).</summary>
    private static void DrawLeavesTile(Color[] pixels, int tile, Color baseColor, Color shadow)
    {
        var rng = RngFor(tile);
        for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
                SetPixel(pixels, tile, x, y,
                    rng.Next(10) == 0 ? shadow : Jitter(rng, baseColor, 20));
    }
}
