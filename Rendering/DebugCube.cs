using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.Rendering;

/// <summary>
/// Phase 1 placeholder: a single colored unit cube at the origin, drawn with BasicEffect.
/// Proves the camera matrices and face winding. Replaced by chunk meshes in Phase 2.
/// </summary>
public class DebugCube
{
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;
    private readonly BasicEffect _effect;

    public DebugCube(GraphicsDevice device)
    {
        const float h = 0.5f;

        // Each face: 4 corners wound clockwise when viewed from outside the cube,
        // so they survive the default CullCounterClockwiseFace rasterizer state.
        var faces = new (Vector3[] Corners, Color Color)[]
        {
            // Top (+Y) — grass green
            (new[] { new Vector3(-h, h, -h), new Vector3(h, h, -h), new Vector3(h, h, h), new Vector3(-h, h, h) }, new Color(96, 176, 64)),
            // Bottom (-Y) — dark dirt
            (new[] { new Vector3(h, -h, -h), new Vector3(-h, -h, -h), new Vector3(-h, -h, h), new Vector3(h, -h, h) }, new Color(84, 58, 38)),
            // North (-Z) — dirt
            (new[] { new Vector3(-h, -h, -h), new Vector3(h, -h, -h), new Vector3(h, h, -h), new Vector3(-h, h, -h) }, new Color(134, 96, 67)),
            // South (+Z) — lighter dirt
            (new[] { new Vector3(h, -h, h), new Vector3(-h, -h, h), new Vector3(-h, h, h), new Vector3(h, h, h) }, new Color(150, 108, 74)),
            // East (+X) — tan
            (new[] { new Vector3(h, -h, -h), new Vector3(h, -h, h), new Vector3(h, h, h), new Vector3(h, h, -h) }, new Color(168, 121, 83)),
            // West (-X) — darker tan
            (new[] { new Vector3(-h, -h, h), new Vector3(-h, -h, -h), new Vector3(-h, h, -h), new Vector3(-h, h, h) }, new Color(117, 84, 58)),
        };

        var vertices = new VertexPositionColor[faces.Length * 4];
        var indices = new ushort[faces.Length * 6];
        for (int f = 0; f < faces.Length; f++)
        {
            var (corners, color) = faces[f];
            int v = f * 4;
            for (int i = 0; i < 4; i++)
                vertices[v + i] = new VertexPositionColor(corners[i], color);

            int t = f * 6;
            indices[t + 0] = (ushort)(v + 0);
            indices[t + 1] = (ushort)(v + 1);
            indices[t + 2] = (ushort)(v + 2);
            indices[t + 3] = (ushort)(v + 0);
            indices[t + 4] = (ushort)(v + 2);
            indices[t + 5] = (ushort)(v + 3);
        }

        _vertexBuffer = new VertexBuffer(device, VertexPositionColor.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
        _vertexBuffer.SetData(vertices);
        _indexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        _indexBuffer.SetData(indices);

        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
    {
        _effect.World = Matrix.Identity;
        _effect.View = view;
        _effect.Projection = projection;

        device.SetVertexBuffer(_vertexBuffer);
        device.Indices = _indexBuffer;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _indexBuffer.IndexCount / 3);
        }
    }
}
