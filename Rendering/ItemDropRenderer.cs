using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.Items;

namespace MinecraftClone.Rendering;

/// <summary>
/// Draws ItemDrops as quarter-size cubes textured with the item's icon tile,
/// slowly spinning and bobbing on their age — visually distinct from falling
/// blocks, which are full-size and static.
/// </summary>
public class ItemDropRenderer
{
    private const float CubeSize = 0.25f;

    private static readonly Vector3[][] FaceCorners =
    {
        new[] { new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(0, 1, 1) },
        new[] { new Vector3(1, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1) },
        new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) },
        new[] { new Vector3(1, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(1, 1, 1) },
        new[] { new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) },
        new[] { new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 1) },
    };

    private static readonly float[] FaceShade = { 1f, 0.5f, 0.8f, 0.8f, 0.65f, 0.65f };

    private readonly BasicEffect _effect;
    private readonly VertexPositionColorTexture[] _vertices = new VertexPositionColorTexture[24];
    private readonly short[] _indices = new short[36];
    private ItemType _currentItem = ItemType.None;

    public ItemDropRenderer(GraphicsDevice device, TextureAtlas atlas)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
            TextureEnabled = true,
            Texture = atlas.Texture,
            FogEnabled = true,
            FogColor = Color.CornflowerBlue.ToVector3(),
            FogStart = 70f,
            FogEnd = 122f,
        };

        for (int face = 0; face < 6; face++)
        {
            byte shade = (byte)(255 * FaceShade[face]);
            var color = new Color(shade, shade, shade);
            for (int i = 0; i < 4; i++)
                _vertices[face * 4 + i] = new VertexPositionColorTexture(
                    (FaceCorners[face][i] - new Vector3(0.5f)) * CubeSize, color, Vector2.Zero);

            int v = face * 4, t = face * 6;
            _indices[t + 0] = (short)(v + 0);
            _indices[t + 1] = (short)(v + 1);
            _indices[t + 2] = (short)(v + 2);
            _indices[t + 3] = (short)(v + 0);
            _indices[t + 4] = (short)(v + 2);
            _indices[t + 5] = (short)(v + 3);
        }
    }

    /// <summary>Per-frame scene light (day/night dimming) and fog/sky color.</summary>
    public void SetEnvironment(Vector3 light, Color sky)
    {
        _effect.DiffuseColor = light;
        _effect.FogColor = sky.ToVector3();
    }

    public void Draw(GraphicsDevice device, FirstPersonCamera camera, IReadOnlyList<ItemDrops.Drop> drops)
    {
        if (drops.Count == 0)
            return;

        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.SamplerStates[0] = SamplerState.PointClamp;

        _effect.View = camera.View;
        _effect.Projection = camera.Projection;

        foreach (var drop in drops)
        {
            if (drop.Item != _currentItem)
            {
                SetItemUVs(drop.Item);
                _currentItem = drop.Item;
            }

            float bob = MathF.Sin(drop.Age * 2.5f) * 0.06f;
            _effect.World = Matrix.CreateRotationY(drop.Age * 1.6f)
                * Matrix.CreateTranslation(drop.Position + new Vector3(0f, bob, 0f));
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertices, 0, 24, _indices, 0, 12);
            }
        }
    }

    private void SetItemUVs(ItemType item)
    {
        var uv = TextureAtlas.GetUVBounds(ItemInfo.GetIconTile(item));
        for (int face = 0; face < 6; face++)
        {
            _vertices[face * 4 + 0].TextureCoordinate = new Vector2(uv.X, uv.W);
            _vertices[face * 4 + 1].TextureCoordinate = new Vector2(uv.Z, uv.W);
            _vertices[face * 4 + 2].TextureCoordinate = new Vector2(uv.Z, uv.Y);
            _vertices[face * 4 + 3].TextureCoordinate = new Vector2(uv.X, uv.Y);
        }
    }
}
