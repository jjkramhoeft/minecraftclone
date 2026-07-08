using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

/// <summary>
/// Draws Mobs as quadruped box-models in the PlayerModel style: body, head,
/// and four legs whose pivot-relative vertices are rotated for the walk swing.
/// One shared part set per mob kind, re-drawn per mob with different matrices.
/// </summary>
public class MobRenderer
{
    private static readonly float[] FaceShade = { 1f, 0.5f, 0.8f, 0.8f, 0.65f, 0.65f };

    private static readonly Vector3[][] FaceCorners =
    {
        new[] { new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(0, 1, 1) },
        new[] { new Vector3(1, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1) },
        new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) },
        new[] { new Vector3(1, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(1, 1, 1) },
        new[] { new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) },
        new[] { new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 1) },
    };

    private record Part(VertexPositionColorTexture[] Vertices, short[] Indices, Vector3 Pivot);

    // Legs come in (pivot, phase-sign) pairs so diagonal legs swing together.
    private record Model(Part Body, Part Head, (Part Part, float Sign)[] Legs);

    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly Dictionary<MobKind, Model> _models = new();

    public MobRenderer(GraphicsDevice device, TextureAtlas atlas)
    {
        _device = device;
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

        _models[MobKind.Pig] = BuildQuadruped(
            tile: BlockInfo.TilePig,
            bodySize: new Vector3(0.55f, 0.45f, 0.9f), legHeight: 0.3f,
            headSize: 0.4f);
        _models[MobKind.Chicken] = BuildQuadruped(
            tile: BlockInfo.TileChicken,
            bodySize: new Vector3(0.35f, 0.3f, 0.5f), legHeight: 0.25f,
            headSize: 0.25f);
    }

    /// <summary>Per-frame scene light (day/night dimming) and fog/sky color.</summary>
    public void SetEnvironment(Vector3 light, Color sky)
    {
        _effect.DiffuseColor = light;
        _effect.FogColor = sky.ToVector3();
    }

    public void Draw(FirstPersonCamera camera, IReadOnlyList<Mobs.Mob> mobs)
    {
        if (mobs.Count == 0)
            return;

        _device.BlendState = BlendState.Opaque;
        _device.DepthStencilState = DepthStencilState.Default;
        _device.RasterizerState = RasterizerState.CullCounterClockwise;
        _device.SamplerStates[0] = SamplerState.PointClamp;

        _effect.View = camera.View;
        _effect.Projection = camera.Projection;

        foreach (var mob in mobs)
        {
            var model = _models[mob.Kind];
            var body = Matrix.CreateRotationY(mob.Yaw) * Matrix.CreateTranslation(mob.Position);
            float swing = MathF.Sin(mob.WalkPhase) * 0.6f;

            DrawPart(model.Body, Matrix.Identity, body);
            DrawPart(model.Head, Matrix.Identity, body);
            foreach (var (leg, sign) in model.Legs)
                DrawPart(leg, Matrix.CreateRotationX(swing * sign), body);
        }
    }

    private void DrawPart(Part part, Matrix rotation, Matrix body)
    {
        _effect.World = rotation * Matrix.CreateTranslation(part.Pivot) * body;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                part.Vertices, 0, part.Vertices.Length, part.Indices, 0, part.Indices.Length / 3);
        }
    }

    /// <summary>+Z is the mob's forward. The body floats on the legs; the head
    /// hangs off the front-top of the body.</summary>
    private static Model BuildQuadruped(int tile, Vector3 bodySize, float legHeight, float headSize)
    {
        var body = BuildBox(
            new Vector3(-bodySize.X / 2, 0f, -bodySize.Z / 2),
            new Vector3(bodySize.X / 2, bodySize.Y, bodySize.Z / 2),
            tile, new Vector3(0f, legHeight, 0f));

        var head = BuildBox(
            new Vector3(-headSize / 2, 0f, 0f),
            new Vector3(headSize / 2, headSize, headSize),
            tile, new Vector3(0f, legHeight + bodySize.Y - headSize / 3, bodySize.Z / 2 - headSize / 3));

        float legHalf = Math.Min(0.09f, bodySize.X / 4);
        float legX = bodySize.X / 2 - legHalf;
        float legZ = bodySize.Z / 2 - legHalf;
        (Part, float) Leg(float x, float z, float sign) => (BuildBox(
            new Vector3(-legHalf, -legHeight, -legHalf),
            new Vector3(legHalf, 0f, legHalf),
            tile, new Vector3(x, legHeight, z)), sign);

        return new Model(body, head, new[]
        {
            Leg(legX, legZ, 1f), Leg(-legX, legZ, -1f),
            Leg(legX, -legZ, -1f), Leg(-legX, -legZ, 1f),
        });
    }

    private static Part BuildBox(Vector3 min, Vector3 max, int tile, Vector3 pivot)
    {
        var vertices = new VertexPositionColorTexture[24];
        var indices = new short[36];
        var size = max - min;
        var uv = TextureAtlas.GetUVBounds(tile);

        for (int face = 0; face < 6; face++)
        {
            byte shade = (byte)(255 * FaceShade[face]);
            var color = new Color(shade, shade, shade);
            Span<Vector2> uvs = stackalloc Vector2[]
            {
                new(uv.X, uv.W), new(uv.Z, uv.W), new(uv.Z, uv.Y), new(uv.X, uv.Y),
            };

            for (int i = 0; i < 4; i++)
                vertices[face * 4 + i] = new VertexPositionColorTexture(min + FaceCorners[face][i] * size, color, uvs[i]);

            int v = face * 4, t = face * 6;
            indices[t + 0] = (short)(v + 0);
            indices[t + 1] = (short)(v + 1);
            indices[t + 2] = (short)(v + 2);
            indices[t + 3] = (short)(v + 0);
            indices[t + 4] = (short)(v + 2);
            indices[t + 5] = (short)(v + 3);
        }

        return new Part(vertices, indices, pivot);
    }
}
