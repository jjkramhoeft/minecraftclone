using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

/// <summary>
/// Draws the crack texture over the block being mined: a unit cube scaled a
/// hair past the block (no z-fighting), textured with one of the four crack
/// tiles picked by break progress, alpha-blended over the terrain.
/// </summary>
public class BreakingOverlay
{
    // Same face tables as ChunkMesher (clockwise from outside).
    private static readonly Vector3[][] FaceCorners =
    {
        new[] { new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(0, 1, 1) },
        new[] { new Vector3(1, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1) },
        new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) },
        new[] { new Vector3(1, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(1, 1, 1) },
        new[] { new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) },
        new[] { new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 1) },
    };

    private readonly BasicEffect _effect;
    private readonly VertexPositionColorTexture[] _vertices = new VertexPositionColorTexture[24];
    private readonly short[] _indices = new short[36];
    private int _currentStage = -1;

    public BreakingOverlay(GraphicsDevice device, TextureAtlas atlas)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
            TextureEnabled = true,
            Texture = atlas.Texture,
        };

        for (int face = 0; face < 6; face++)
        {
            for (int i = 0; i < 4; i++)
                _vertices[face * 4 + i] = new VertexPositionColorTexture(FaceCorners[face][i], Color.White, Vector2.Zero);

            int v = face * 4, t = face * 6;
            _indices[t + 0] = (short)(v + 0);
            _indices[t + 1] = (short)(v + 1);
            _indices[t + 2] = (short)(v + 2);
            _indices[t + 3] = (short)(v + 0);
            _indices[t + 4] = (short)(v + 2);
            _indices[t + 5] = (short)(v + 3);
        }
    }

    public void Draw(GraphicsDevice device, FirstPersonCamera camera, (int X, int Y, int Z) block, float progress)
    {
        if (progress <= 0f)
            return;

        int stage = Math.Clamp((int)(progress * 4), 0, 3);
        if (stage != _currentStage)
        {
            SetStageUVs(BlockInfo.TileCrack0 + stage);
            _currentStage = stage;
        }

        device.BlendState = BlendState.NonPremultiplied;
        device.DepthStencilState = DepthStencilState.DepthRead;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.SamplerStates[0] = SamplerState.PointClamp;

        // Scale about the cube center so the overlay floats just outside the block.
        _effect.World = Matrix.CreateTranslation(-0.5f, -0.5f, -0.5f)
            * Matrix.CreateScale(1.002f)
            * Matrix.CreateTranslation(block.X + 0.5f, block.Y + 0.5f, block.Z + 0.5f);
        _effect.View = camera.View;
        _effect.Projection = camera.Projection;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertices, 0, 24, _indices, 0, 12);
        }

        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.Default;
    }

    private void SetStageUVs(int tile)
    {
        var uv = TextureAtlas.GetUVBounds(tile);
        for (int face = 0; face < 6; face++)
        {
            _vertices[face * 4 + 0].TextureCoordinate = new Vector2(uv.X, uv.W);
            _vertices[face * 4 + 1].TextureCoordinate = new Vector2(uv.Z, uv.W);
            _vertices[face * 4 + 2].TextureCoordinate = new Vector2(uv.Z, uv.Y);
            _vertices[face * 4 + 3].TextureCoordinate = new Vector2(uv.X, uv.Y);
        }
    }
}
