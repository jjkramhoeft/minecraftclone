using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

/// <summary>
/// GPU-side mesh for one chunk. Must be created and disposed on the main thread
/// (the only thread allowed to touch the GraphicsDevice).
/// </summary>
public class ChunkMesh : IDisposable
{
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;

    public ChunkCoord Coord { get; }
    public bool IsEmpty => _indexBuffer == null;

    public Matrix World => Matrix.CreateTranslation(Coord.X * Chunk.SizeX, 0f, Coord.Z * Chunk.SizeZ);

    public ChunkMesh(GraphicsDevice device, ChunkCoord coord, MeshData data)
    {
        Coord = coord;
        if (data.IsEmpty)
            return;

        _vertexBuffer = new VertexBuffer(device, VertexPositionColorTexture.VertexDeclaration, data.Vertices.Length, BufferUsage.WriteOnly);
        _vertexBuffer.SetData(data.Vertices);
        _indexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, data.Indices.Length, BufferUsage.WriteOnly);
        _indexBuffer.SetData(data.Indices);
    }

    public void Draw(GraphicsDevice device)
    {
        if (IsEmpty)
            return;

        device.SetVertexBuffer(_vertexBuffer);
        device.Indices = _indexBuffer;
        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _indexBuffer.IndexCount / 3);
    }

    public void Dispose()
    {
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
    }
}
