using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

/// <summary>
/// Draws <see cref="Particles"/> as small camera-facing quads — colored only,
/// no texture — alpha-fading over each particle's life. Depth-tested against
/// the world but without writing depth, so overlapping particles blend cleanly
/// and never occlude the terrain behind them.
/// </summary>
public class ParticleRenderer
{
    private readonly BasicEffect _effect;
    private readonly VertexPositionColor[] _vertices = new VertexPositionColor[Particles.Capacity * 6];

    public ParticleRenderer(GraphicsDevice device)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };
    }

    public void Draw(GraphicsDevice device, FirstPersonCamera camera, Particles particles)
    {
        int count = particles.Count;
        if (count == 0)
            return;

        // Billboard basis: the camera's right/up axes are the first two rows of
        // the view rotation.
        var view = camera.View;
        var right = new Vector3(view.M11, view.M21, view.M31);
        var up = new Vector3(view.M12, view.M22, view.M32);

        var pool = particles.Pool;
        int v = 0;
        for (int i = 0; i < count; i++)
        {
            var p = pool[i];
            float fade = 1f - p.Age / p.Life;
            var color = p.Color * fade;
            var r = right * p.Size;
            var u = up * p.Size;
            var c = p.Position;
            _vertices[v++] = new VertexPositionColor(c - r - u, color);
            _vertices[v++] = new VertexPositionColor(c + r - u, color);
            _vertices[v++] = new VertexPositionColor(c + r + u, color);
            _vertices[v++] = new VertexPositionColor(c - r - u, color);
            _vertices[v++] = new VertexPositionColor(c + r + u, color);
            _vertices[v++] = new VertexPositionColor(c - r + u, color);
        }

        device.BlendState = BlendState.NonPremultiplied;
        device.DepthStencilState = DepthStencilState.DepthRead;
        device.RasterizerState = RasterizerState.CullNone;

        _effect.World = Matrix.Identity;
        _effect.View = view;
        _effect.Projection = camera.Projection;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleList, _vertices, 0, count * 2);
        }

        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.Default;
    }
}
