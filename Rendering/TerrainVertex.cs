using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.Rendering;

/// <summary>
/// Vertex for greedy-meshed terrain: LocalUV is in block units and unbounded
/// (a quad merged over 5 blocks spans 0..5); TileOrigin is the tile's top-left
/// corner in atlas UV space. TerrainEffect wraps LocalUV with frac() so the
/// tile repeats per block across merged quads.
/// </summary>
public struct TerrainVertex : IVertexType
{
    public Vector3 Position;
    public Color Color;
    public Vector2 LocalUV;
    public Vector2 TileOrigin;

    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Color, VertexElementUsage.Color, 0),
        new VertexElement(16, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1));

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

    public TerrainVertex(Vector3 position, Color color, Vector2 localUV, Vector2 tileOrigin)
    {
        Position = position;
        Color = color;
        LocalUV = localUV;
        TileOrigin = tileOrigin;
    }
}
