using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

/// <summary>CPU-side mesh: safe to build on a worker thread, uploaded to the GPU by ChunkMesh.</summary>
public record MeshData(VertexPositionColorTexture[] Vertices, int[] Indices)
{
    public bool IsEmpty => Indices.Length == 0;
}

/// <summary>
/// Naive culled meshing: one quad per solid-block face that borders a non-solid block.
/// Vertices are in chunk-local coordinates; ChunkMesh translates to world position.
/// Pure CPU work — no GraphicsDevice access, so it can run on worker threads.
/// </summary>
public static class ChunkMesher
{
    // Indexed by BlockFace: Top, Bottom, North(-Z), South(+Z), East(+X), West(-X).
    private static readonly (int X, int Y, int Z)[] FaceNormals =
    {
        (0, 1, 0), (0, -1, 0), (0, 0, -1), (0, 0, 1), (1, 0, 0), (-1, 0, 0),
    };

    // Corner offsets from the block's min corner, wound clockwise viewed from
    // outside (front-facing under the default CullCounterClockwiseFace state).
    private static readonly Vector3[][] FaceCorners =
    {
        new[] { new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(0, 1, 1) }, // Top
        new[] { new Vector3(1, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1) }, // Bottom
        new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) }, // North
        new[] { new Vector3(1, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(1, 1, 1) }, // South
        new[] { new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) }, // East
        new[] { new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 1) }, // West
    };

    // Fake directional light: constant brightness per face orientation.
    private static readonly float[] FaceShade = { 1f, 0.5f, 0.8f, 0.8f, 0.65f, 0.65f };

    /// <param name="chunk">The chunk to mesh.</param>
    /// <param name="getOutsideBlock">
    /// Sampler for chunk-local coordinates outside this chunk's bounds (border culling).
    /// Phase 2 passes "always Air"; Phase 3 resolves through neighboring chunks.
    /// </param>
    public static MeshData Build(Chunk chunk, Func<int, int, int, BlockType> getOutsideBlock)
    {
        var vertices = new List<VertexPositionColorTexture>();
        var indices = new List<int>();

        for (int y = 0; y < Chunk.SizeY; y++)
        {
            for (int z = 0; z < Chunk.SizeZ; z++)
            {
                for (int x = 0; x < Chunk.SizeX; x++)
                {
                    var type = chunk.GetBlock(x, y, z);
                    if (type == BlockType.Air)
                        continue;

                    for (int face = 0; face < 6; face++)
                    {
                        var (nx, ny, nz) = FaceNormals[face];
                        int bx = x + nx, by = y + ny, bz = z + nz;
                        var neighbor = Chunk.InBounds(bx, by, bz)
                            ? chunk.GetBlock(bx, by, bz)
                            : getOutsideBlock(bx, by, bz);
                        if (BlockInfo.IsSolid(neighbor))
                            continue;

                        AddFace(vertices, indices, new Vector3(x, y, z), face, type);
                    }
                }
            }
        }

        return new MeshData(vertices.ToArray(), indices.ToArray());
    }

    private static void AddFace(List<VertexPositionColorTexture> vertices, List<int> indices, Vector3 blockPos, int face, BlockType type)
    {
        // The face brightness rides in the vertex color; BasicEffect multiplies
        // it with the sampled atlas texel.
        byte shade = (byte)(255 * FaceShade[face]);
        var color = new Color(shade, shade, shade);

        var uv = TextureAtlas.GetUVBounds(BlockInfo.GetFaceTile(type, (BlockFace)face));

        // FaceCorners order is (bottom-left, bottom-right, top-right, top-left)
        // for the side faces, so v runs top-of-tile → top-of-block.
        int baseIndex = vertices.Count;
        var corners = FaceCorners[face];
        vertices.Add(new VertexPositionColorTexture(blockPos + corners[0], color, new Vector2(uv.X, uv.W)));
        vertices.Add(new VertexPositionColorTexture(blockPos + corners[1], color, new Vector2(uv.Z, uv.W)));
        vertices.Add(new VertexPositionColorTexture(blockPos + corners[2], color, new Vector2(uv.Z, uv.Y)));
        vertices.Add(new VertexPositionColorTexture(blockPos + corners[3], color, new Vector2(uv.X, uv.Y)));

        indices.Add(baseIndex + 0);
        indices.Add(baseIndex + 1);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 0);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 3);
    }
}
