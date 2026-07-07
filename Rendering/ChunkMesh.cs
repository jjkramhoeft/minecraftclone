using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

/// <summary>
/// GPU-side mesh for one chunk: an opaque buffer set and an optional
/// transparent water buffer set. Must be created and disposed on the main
/// thread (the only thread allowed to touch the GraphicsDevice).
/// </summary>
public class ChunkMesh : IDisposable
{
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;
    private readonly VertexBuffer _waterVertexBuffer;
    private readonly IndexBuffer _waterIndexBuffer;

    public ChunkCoord Coord { get; }
    public BoundingBox Bounds { get; }

    public bool IsEmpty => _indexBuffer == null && _waterIndexBuffer == null;
    public bool HasOpaque => _indexBuffer != null;
    public bool HasWater => _waterIndexBuffer != null;

    public Matrix World => Matrix.CreateTranslation(Coord.X * Chunk.SizeX, 0f, Coord.Z * Chunk.SizeZ);

    public ChunkMesh(GraphicsDevice device, ChunkCoord coord, MeshData data)
    {
        Coord = coord;
        var origin = new Vector3(coord.X * Chunk.SizeX, 0f, coord.Z * Chunk.SizeZ);
        Bounds = new BoundingBox(origin, origin + new Vector3(Chunk.SizeX, Chunk.SizeY, Chunk.SizeZ));

        if (data.Indices.Length > 0)
        {
            _vertexBuffer = new VertexBuffer(device, VertexPositionColorTexture.VertexDeclaration, data.Vertices.Length, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(data.Vertices);
            _indexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, data.Indices.Length, BufferUsage.WriteOnly);
            _indexBuffer.SetData(data.Indices);
        }

        if (data.WaterIndices.Length > 0)
        {
            _waterVertexBuffer = new VertexBuffer(device, VertexPositionColorTexture.VertexDeclaration, data.WaterVertices.Length, BufferUsage.WriteOnly);
            _waterVertexBuffer.SetData(data.WaterVertices);
            _waterIndexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, data.WaterIndices.Length, BufferUsage.WriteOnly);
            _waterIndexBuffer.SetData(data.WaterIndices);
        }
    }

    public void DrawOpaque(GraphicsDevice device)
    {
        if (!HasOpaque)
            return;
        device.SetVertexBuffer(_vertexBuffer);
        device.Indices = _indexBuffer;
        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _indexBuffer.IndexCount / 3);
    }

    public void DrawWater(GraphicsDevice device)
    {
        if (!HasWater)
            return;
        device.SetVertexBuffer(_waterVertexBuffer);
        device.Indices = _waterIndexBuffer;
        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _waterIndexBuffer.IndexCount / 3);
    }

    public void Dispose()
    {
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _waterVertexBuffer?.Dispose();
        _waterIndexBuffer?.Dispose();
    }
}
