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
