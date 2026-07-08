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
    private readonly VertexBuffer _cutoutVertexBuffer;
    private readonly IndexBuffer _cutoutIndexBuffer;
    private readonly VertexBuffer _lightVertexBuffer;
    private readonly IndexBuffer _lightIndexBuffer;

    public ChunkCoord Coord { get; }
    public BoundingBox Bounds { get; }

    /// <summary>Total vertices across all passes — perf telemetry (F3/smoke).</summary>
    public int VertexCount { get; }
    public int OpaqueVertexCount { get; }
    public int WaterVertexCount { get; }
    public int CutoutVertexCount { get; }
    public int LightVertexCount { get; }

    public bool IsEmpty => _indexBuffer == null && _waterIndexBuffer == null
        && _cutoutIndexBuffer == null && _lightIndexBuffer == null;
    public bool HasOpaque => _indexBuffer != null;
    public bool HasWater => _waterIndexBuffer != null;
    public bool HasCutout => _cutoutIndexBuffer != null;
    public bool HasLight => _lightIndexBuffer != null;

    public Matrix World => Matrix.CreateTranslation(Coord.X * Chunk.SizeX, 0f, Coord.Z * Chunk.SizeZ);

    public ChunkMesh(GraphicsDevice device, ChunkCoord coord, MeshData data)
    {
        Coord = coord;
        var origin = new Vector3(coord.X * Chunk.SizeX, 0f, coord.Z * Chunk.SizeZ);
        Bounds = new BoundingBox(origin, origin + new Vector3(Chunk.SizeX, Chunk.SizeY, Chunk.SizeZ));
        VertexCount = data.Vertices.Length + data.WaterVertices.Length
            + data.CutoutVertices.Length + data.LightVertices.Length;
        OpaqueVertexCount = data.Vertices.Length;
        WaterVertexCount = data.WaterVertices.Length;
        CutoutVertexCount = data.CutoutVertices.Length;
        LightVertexCount = data.LightVertices.Length;

        if (data.Indices.Length > 0)
        {
            _vertexBuffer = new VertexBuffer(device, TerrainVertex.VertexDeclaration, data.Vertices.Length, BufferUsage.WriteOnly);
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

        if (data.CutoutIndices.Length > 0)
        {
            _cutoutVertexBuffer = new VertexBuffer(device, VertexPositionColorTexture.VertexDeclaration, data.CutoutVertices.Length, BufferUsage.WriteOnly);
            _cutoutVertexBuffer.SetData(data.CutoutVertices);
            _cutoutIndexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, data.CutoutIndices.Length, BufferUsage.WriteOnly);
            _cutoutIndexBuffer.SetData(data.CutoutIndices);
        }

        if (data.LightIndices.Length > 0)
        {
            _lightVertexBuffer = new VertexBuffer(device, TerrainVertex.VertexDeclaration, data.LightVertices.Length, BufferUsage.WriteOnly);
            _lightVertexBuffer.SetData(data.LightVertices);
            _lightIndexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, data.LightIndices.Length, BufferUsage.WriteOnly);
            _lightIndexBuffer.SetData(data.LightIndices);
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

    public void DrawCutout(GraphicsDevice device)
    {
        if (!HasCutout)
            return;
        device.SetVertexBuffer(_cutoutVertexBuffer);
        device.Indices = _cutoutIndexBuffer;
        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _cutoutIndexBuffer.IndexCount / 3);
    }

    public void DrawLight(GraphicsDevice device)
    {
        if (!HasLight)
            return;
        device.SetVertexBuffer(_lightVertexBuffer);
        device.Indices = _lightIndexBuffer;
        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _lightIndexBuffer.IndexCount / 3);
    }

    public void Dispose()
    {
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _waterVertexBuffer?.Dispose();
        _waterIndexBuffer?.Dispose();
        _cutoutVertexBuffer?.Dispose();
        _cutoutIndexBuffer?.Dispose();
        _lightVertexBuffer?.Dispose();
        _lightIndexBuffer?.Dispose();
    }
}
