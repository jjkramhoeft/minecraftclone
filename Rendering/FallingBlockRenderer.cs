using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

/// <summary>
/// Draws airborne FallingBlocks entries as textured cubes at their continuous
/// positions — visually identical to the block they were in the grid, with the
/// same per-face shading and fog as the terrain.
/// </summary>
public class FallingBlockRenderer
{
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
    private BlockType _currentType = BlockType.Air;

    public FallingBlockRenderer(GraphicsDevice device, TextureAtlas atlas)
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
                _vertices[face * 4 + i] = new VertexPositionColorTexture(FaceCorners[face][i], color, Vector2.Zero);

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

    public void Draw(GraphicsDevice device, FirstPersonCamera camera, System.Collections.Generic.IReadOnlyList<FallingBlocks.Entry> entries)
    {
        if (entries.Count == 0)
            return;

        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.SamplerStates[0] = SamplerState.PointClamp;

        _effect.View = camera.View;
        _effect.Projection = camera.Projection;

        foreach (var entry in entries)
        {
            if (entry.Type != _currentType)
            {
                SetTypeUVs(entry.Type);
                _currentType = entry.Type;
            }

            _effect.World = Matrix.CreateTranslation(entry.X, entry.Y, entry.Z);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertices, 0, 24, _indices, 0, 12);
            }
        }
    }

    private void SetTypeUVs(BlockType type)
    {
        for (int face = 0; face < 6; face++)
        {
            var uv = TextureAtlas.GetUVBounds(BlockInfo.GetFaceTile(type, (BlockFace)face));
            _vertices[face * 4 + 0].TextureCoordinate = new Vector2(uv.X, uv.W);
            _vertices[face * 4 + 1].TextureCoordinate = new Vector2(uv.Z, uv.W);
            _vertices[face * 4 + 2].TextureCoordinate = new Vector2(uv.Z, uv.Y);
            _vertices[face * 4 + 3].TextureCoordinate = new Vector2(uv.X, uv.Y);
        }
    }
}
