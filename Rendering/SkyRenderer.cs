using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

/// <summary>
/// Sun, moon, and stars. Drawn right after the sky clear and before the world,
/// with depth disabled, so terrain always covers them. All geometry is
/// positioned relative to the camera, so the sky never parallaxes as the
/// player moves. Stars share the sun's rotation axis and fade in with
/// DayNightCycle.StarAlpha.
/// </summary>
public class SkyRenderer
{
    private const float SkyDistance = 300f; // well inside the 1000 far plane, outside all terrain
    private const float SunSize = 42f;
    private const float MoonSize = 30f;
    private const int StarCount = 150;

    private readonly BasicEffect _spriteEffect; // textured, for sun/moon
    private readonly BasicEffect _starEffect;   // vertex color only
    private readonly VertexPositionColorTexture[] _quad = new VertexPositionColorTexture[4];
    private readonly short[] _quadIndices = { 0, 1, 2, 0, 2, 3 };

    private readonly Vector3[] _starDirections = new Vector3[StarCount];
    private readonly float[] _starSizes = new float[StarCount];
    private readonly VertexPositionColor[] _starVertices = new VertexPositionColor[StarCount * 6];

    public SkyRenderer(GraphicsDevice device, TextureAtlas atlas)
    {
        _spriteEffect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
            TextureEnabled = true,
            Texture = atlas.Texture,
        };
        _starEffect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };

        // A fixed constellation: same sky in every world.
        var rng = new Random(9001);
        for (int i = 0; i < StarCount; i++)
        {
            Vector3 direction;
            do
            {
                direction = new Vector3(
                    (float)(rng.NextDouble() * 2 - 1),
                    (float)(rng.NextDouble() * 2 - 1),
                    (float)(rng.NextDouble() * 2 - 1));
            } while (direction.LengthSquared() is < 0.05f or > 1f);
            _starDirections[i] = Vector3.Normalize(direction);
            _starSizes[i] = 0.45f + (float)rng.NextDouble() * 0.75f;
        }
    }

    public void Draw(GraphicsDevice device, FirstPersonCamera camera, DayNightCycle cycle)
    {
        device.DepthStencilState = DepthStencilState.None;
        device.BlendState = BlendState.NonPremultiplied;
        device.RasterizerState = RasterizerState.CullNone;
        device.SamplerStates[0] = SamplerState.PointClamp;

        if (cycle.StarAlpha > 0.01f)
            DrawStars(device, camera, cycle);
        DrawSprite(device, camera, BlockInfo.TileSun, cycle.SunDirection, SunSize);
        DrawSprite(device, camera, BlockInfo.TileMoon, cycle.MoonDirection, MoonSize);

        device.DepthStencilState = DepthStencilState.Default;
        device.BlendState = BlendState.Opaque;
    }

    private void DrawStars(GraphicsDevice device, FirstPersonCamera camera, DayNightCycle cycle)
    {
        // Stars ride the same axis the sun orbits, so they drift through the night.
        var rotation = Matrix.CreateRotationZ(-cycle.TimeOfDay * MathHelper.TwoPi);
        var color = Color.White * cycle.StarAlpha;

        for (int i = 0; i < StarCount; i++)
        {
            var direction = Vector3.TransformNormal(_starDirections[i], rotation);
            var center = camera.Position + direction * SkyDistance;
            var (right, up) = TangentBasis(direction);
            right *= _starSizes[i];
            up *= _starSizes[i];

            int v = i * 6;
            _starVertices[v + 0] = new VertexPositionColor(center - right - up, color);
            _starVertices[v + 1] = new VertexPositionColor(center + right - up, color);
            _starVertices[v + 2] = new VertexPositionColor(center + right + up, color);
            _starVertices[v + 3] = new VertexPositionColor(center - right - up, color);
            _starVertices[v + 4] = new VertexPositionColor(center + right + up, color);
            _starVertices[v + 5] = new VertexPositionColor(center - right + up, color);
        }

        _starEffect.World = Matrix.Identity;
        _starEffect.View = camera.View;
        _starEffect.Projection = camera.Projection;
        foreach (var pass in _starEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleList, _starVertices, 0, StarCount * 2);
        }
    }

    private void DrawSprite(GraphicsDevice device, FirstPersonCamera camera, int tile, Vector3 direction, float size)
    {
        var center = camera.Position + direction * SkyDistance;
        var (right, up) = TangentBasis(direction);
        right *= size / 2f;
        up *= size / 2f;

        var uv = TextureAtlas.GetUVBounds(tile);
        _quad[0] = new VertexPositionColorTexture(center - right - up, Color.White, new Vector2(uv.X, uv.W));
        _quad[1] = new VertexPositionColorTexture(center + right - up, Color.White, new Vector2(uv.Z, uv.W));
        _quad[2] = new VertexPositionColorTexture(center + right + up, Color.White, new Vector2(uv.Z, uv.Y));
        _quad[3] = new VertexPositionColorTexture(center - right + up, Color.White, new Vector2(uv.X, uv.Y));

        _spriteEffect.World = Matrix.Identity;
        _spriteEffect.View = camera.View;
        _spriteEffect.Projection = camera.Projection;
        foreach (var pass in _spriteEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _quad, 0, 4, _quadIndices, 0, 2);
        }
    }

    /// <summary>Two unit vectors perpendicular to the direction — the plane the
    /// sprite quad lives in.</summary>
    private static (Vector3 Right, Vector3 Up) TangentBasis(Vector3 direction)
    {
        var reference = MathF.Abs(direction.Y) > 0.99f ? Vector3.UnitX : Vector3.Up;
        var right = Vector3.Normalize(Vector3.Cross(direction, reference));
        var up = Vector3.Cross(right, direction);
        return (right, up);
    }
}
