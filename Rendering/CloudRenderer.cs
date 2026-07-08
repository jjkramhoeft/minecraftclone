using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

/// <summary>
/// Craft-style clouds: flat translucent quads on a coarse cell grid at a fixed
/// height, selected by 2D noise over cell coordinates so they form stable
/// clumps, drifting slowly along +X with time. Drawn after the world with
/// depth-read so mountains occlude clouds from below and clouds cover terrain
/// when flying above them. Vertices are rebuilt in a preallocated buffer each
/// frame — no per-frame garbage.
/// </summary>
public class CloudRenderer
{
    private const float CloudY = 100f;
    private const float CellSize = 12f;
    private const int CellRadius = 20;          // cells drawn each way from the camera
    private const float DriftSpeed = 1.2f;      // blocks per second along +X
    private const float CoverageThreshold = 0.3f; // noise above this is cloud
    private const byte MaxAlpha = 140;

    private readonly BasicEffect _effect;
    private readonly FastNoiseLite _noise;
    private readonly VertexPositionColor[] _vertices =
        new VertexPositionColor[(2 * CellRadius + 1) * (2 * CellRadius + 1) * 6];

    private float _time;

    public CloudRenderer(GraphicsDevice device)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };
        // Same sky in every world, like the star field.
        _noise = new FastNoiseLite(4242);
        _noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        _noise.SetFrequency(0.12f); // clumps a handful of cells across
    }

    public void Update(float dt) => _time += dt;

    /// <summary>Whether a cloud cell covers the given view direction from the
    /// camera (1 = covered, 0 = clear sky). The star pass uses this to hide
    /// stars behind clouds even though the cloud quads are only translucent.
    /// Mirrors the cell/noise mapping in <see cref="Draw"/>.</summary>
    public float CoverageAt(Vector3 cameraPos, Vector3 direction)
    {
        if (direction.Y <= 0.001f)
            return 0f; // not looking up toward the layer
        float t = (CloudY - cameraPos.Y) / direction.Y;
        if (t <= 0f)
            return 0f; // the layer is below/behind the view (e.g. flying above it)

        float drift = _time * DriftSpeed;
        float wx = cameraPos.X + direction.X * t;
        float wz = cameraPos.Z + direction.Z * t;
        int cx = (int)MathF.Floor((wx - drift) / CellSize);
        int cz = (int)MathF.Floor(wz / CellSize);
        return _noise.GetNoise(cx, cz) >= CoverageThreshold ? 1f : 0f;
    }

    /// <param name="light">Day/night scene light — clouds dim at night too.</param>
    public void Draw(GraphicsDevice device, FirstPersonCamera camera, Vector3 light)
    {
        float drift = _time * DriftSpeed;
        // The grid is anchored in noise space, so cells keep their shape while
        // the whole layer slides; the camera term keeps coverage centered.
        int centerX = (int)MathF.Floor((camera.Position.X - drift) / CellSize);
        int centerZ = (int)MathF.Floor(camera.Position.Z / CellSize);

        byte r = (byte)(255 * light.X), g = (byte)(255 * light.Y), b = (byte)(255 * light.Z);
        int v = 0;
        for (int dz = -CellRadius; dz <= CellRadius; dz++)
        {
            for (int dx = -CellRadius; dx <= CellRadius; dx++)
            {
                int cx = centerX + dx, cz = centerZ + dz;
                if (_noise.GetNoise(cx, cz) < CoverageThreshold)
                    continue;

                // Fade toward the draw radius so the layer has no hard edge.
                float edge = MathF.Sqrt(dx * dx + dz * dz) / CellRadius;
                float fade = MathHelper.Clamp((1f - edge) * 3f, 0f, 1f);
                if (fade <= 0f)
                    continue;
                var color = new Color(r, g, b, (byte)(MaxAlpha * fade));

                float x0 = cx * CellSize + drift, z0 = cz * CellSize;
                float x1 = x0 + CellSize, z1 = z0 + CellSize;
                _vertices[v++] = new VertexPositionColor(new Vector3(x0, CloudY, z0), color);
                _vertices[v++] = new VertexPositionColor(new Vector3(x1, CloudY, z0), color);
                _vertices[v++] = new VertexPositionColor(new Vector3(x1, CloudY, z1), color);
                _vertices[v++] = new VertexPositionColor(new Vector3(x0, CloudY, z0), color);
                _vertices[v++] = new VertexPositionColor(new Vector3(x1, CloudY, z1), color);
                _vertices[v++] = new VertexPositionColor(new Vector3(x0, CloudY, z1), color);
            }
        }
        if (v == 0)
            return;

        device.BlendState = BlendState.NonPremultiplied;
        device.DepthStencilState = DepthStencilState.DepthRead;
        device.RasterizerState = RasterizerState.CullNone; // visible from above and below

        _effect.World = Matrix.Identity;
        _effect.View = camera.View;
        _effect.Projection = camera.Projection;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleList, _vertices, 0, v / 3);
        }

        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
    }
}
