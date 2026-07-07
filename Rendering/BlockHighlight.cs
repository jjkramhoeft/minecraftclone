using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.Rendering;

/// <summary>Black wireframe outline around the block the player is looking at.</summary>
public class BlockHighlight
{
    private readonly BasicEffect _effect;
    private readonly VertexPositionColor[] _lines;

    public BlockHighlight(GraphicsDevice device)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };

        // Slightly inflated unit cube so the lines don't z-fight the block faces.
        const float lo = -0.004f, hi = 1.004f;
        var c = new Vector3[]
        {
            new(lo, lo, lo), new(hi, lo, lo), new(hi, lo, hi), new(lo, lo, hi), // bottom corners
            new(lo, hi, lo), new(hi, hi, lo), new(hi, hi, hi), new(lo, hi, hi), // top corners
        };
        int[] edges = { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 };

        _lines = new VertexPositionColor[edges.Length];
        var color = new Color(20, 20, 20);
        for (int i = 0; i < edges.Length; i++)
            _lines[i] = new VertexPositionColor(c[edges[i]], color);
    }

    public void Draw(GraphicsDevice device, FirstPersonCamera camera, int x, int y, int z)
    {
        _effect.World = Matrix.CreateTranslation(x, y, z);
        _effect.View = camera.View;
        _effect.Projection = camera.Projection;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.LineList, _lines, 0, _lines.Length / 2);
        }
    }
}
