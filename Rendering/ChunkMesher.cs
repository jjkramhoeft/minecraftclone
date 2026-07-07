using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

/// <summary>CPU-side mesh: safe to build on a worker thread, uploaded to the GPU by ChunkMesh.</summary>
public record MeshData(
    VertexPositionColorTexture[] Vertices, int[] Indices,
    VertexPositionColorTexture[] WaterVertices, int[] WaterIndices,
    VertexPositionColorTexture[] CutoutVertices, int[] CutoutIndices)
{
    public bool IsEmpty => Indices.Length == 0 && WaterIndices.Length == 0 && CutoutIndices.Length == 0;
}

/// <summary>
/// Naive culled meshing: one quad per block face that borders a non-solid block,
/// with per-vertex ambient occlusion. Water goes into a separate vertex list so
/// the renderer can draw it in a transparent pass after the opaque terrain.
/// Vertices are in chunk-local coordinates; ChunkMesh translates to world
/// position. Pure CPU work — no GraphicsDevice access, so it can run on worker
/// threads. The outside sampler must handle diagonal excursions (AO reads
/// corner neighbors), i.e. up to one chunk away in both X and Z at once.
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
    // Side faces run (bottom-left, bottom-right, top-right, top-left) so the
    // texture v axis maps top-of-tile → top-of-block.
    private static readonly Vector3[][] FaceCorners =
    {
        new[] { new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(0, 1, 1) }, // Top
        new[] { new Vector3(1, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1) }, // Bottom
        new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) }, // North
        new[] { new Vector3(1, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(1, 1, 1) }, // South
        new[] { new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) }, // East
        new[] { new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 1) }, // West
    };

    // The two in-plane axes per face (0=X, 1=Y, 2=Z), used for AO neighbor lookups.
    private static readonly (int U, int V)[] FaceTangents =
    {
        (0, 2), (0, 2), (0, 1), (0, 1), (1, 2), (1, 2),
    };

    // Fake directional light: constant brightness per face orientation.
    private static readonly float[] FaceShade = { 1f, 0.5f, 0.8f, 0.8f, 0.65f, 0.65f };

    // Brightness per ambient-occlusion level (0 = fully occluded corner).
    private static readonly float[] AoFactor = { 0.55f, 0.7f, 0.85f, 1f };

    private const byte WaterAlpha = 160;

    /// <param name="chunk">The chunk to mesh.</param>
    /// <param name="getOutsideBlock">
    /// Sampler for chunk-local coordinates outside this chunk's bounds — must
    /// resolve all 8 surrounding chunks (AO samples diagonals).
    /// </param>
    public static MeshData Build(Chunk chunk, Func<int, int, int, BlockType> getOutsideBlock)
    {
        var vertices = new List<VertexPositionColorTexture>();
        var indices = new List<int>();
        var waterVertices = new List<VertexPositionColorTexture>();
        var waterIndices = new List<int>();
        var cutoutVertices = new List<VertexPositionColorTexture>();
        var cutoutIndices = new List<int>();

        BlockType Sample(int x, int y, int z) =>
            Chunk.InBounds(x, y, z) ? chunk.GetBlock(x, y, z) : getOutsideBlock(x, y, z);

        for (int y = 0; y < Chunk.SizeY; y++)
        {
            for (int z = 0; z < Chunk.SizeZ; z++)
            {
                for (int x = 0; x < Chunk.SizeX; x++)
                {
                    var type = chunk.GetBlock(x, y, z);
                    if (type == BlockType.Air)
                        continue;

                    if (BlockInfo.IsFlower(type))
                    {
                        // Flowers are crossed quads in the cutout pass — no
                        // faces, no culling, no AO, no neighbor reads.
                        AddCrossQuads(cutoutVertices, cutoutIndices, x, y, z, type);
                        continue;
                    }

                    for (int face = 0; face < 6; face++)
                    {
                        var (nx, ny, nz) = FaceNormals[face];
                        var neighbor = Sample(x + nx, y + ny, z + nz);

                        if (type == BlockType.Water)
                        {
                            // Water surfaces only show against air; solid
                            // neighbors hide it and water-water is interior.
                            if (neighbor == BlockType.Air)
                                AddWaterFace(waterVertices, waterIndices, x, y, z, face);
                        }
                        else if (!BlockInfo.IsSolid(neighbor))
                        {
                            AddFace(vertices, indices, x, y, z, face, type, Sample);
                        }
                    }
                }
            }
        }

        return new MeshData(
            vertices.ToArray(), indices.ToArray(),
            waterVertices.ToArray(), waterIndices.ToArray(),
            cutoutVertices.ToArray(), cutoutIndices.ToArray());
    }

    // The two diagonal quads of a flower, inset from the block edges. Drawn
    // with CullNone, so a single winding per quad shows both sides.
    private static readonly Vector3[][] CrossQuads =
    {
        new[] { new Vector3(0.15f, 0, 0.15f), new Vector3(0.85f, 0, 0.85f), new Vector3(0.85f, 1, 0.85f), new Vector3(0.15f, 1, 0.15f) },
        new[] { new Vector3(0.85f, 0, 0.15f), new Vector3(0.15f, 0, 0.85f), new Vector3(0.15f, 1, 0.85f), new Vector3(0.85f, 1, 0.15f) },
    };

    private static void AddCrossQuads(List<VertexPositionColorTexture> vertices, List<int> indices, int bx, int by, int bz, BlockType type)
    {
        var uv = TextureAtlas.GetUVBounds(BlockInfo.GetFaceTile(type, BlockFace.South));
        var blockPos = new Vector3(bx, by, bz);

        foreach (var quad in CrossQuads)
        {
            int baseIndex = vertices.Count;
            vertices.Add(new VertexPositionColorTexture(blockPos + quad[0], Color.White, new Vector2(uv.X, uv.W)));
            vertices.Add(new VertexPositionColorTexture(blockPos + quad[1], Color.White, new Vector2(uv.Z, uv.W)));
            vertices.Add(new VertexPositionColorTexture(blockPos + quad[2], Color.White, new Vector2(uv.Z, uv.Y)));
            vertices.Add(new VertexPositionColorTexture(blockPos + quad[3], Color.White, new Vector2(uv.X, uv.Y)));

            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 3);
        }
    }

    private static void AddFace(List<VertexPositionColorTexture> vertices, List<int> indices,
        int bx, int by, int bz, int face, BlockType type, Func<int, int, int, BlockType> sample)
    {
        float shade = FaceShade[face];
        var uv = TextureAtlas.GetUVBounds(BlockInfo.GetFaceTile(type, (BlockFace)face));
        Span<Vector2> uvs = stackalloc Vector2[]
        {
            new(uv.X, uv.W), new(uv.Z, uv.W), new(uv.Z, uv.Y), new(uv.X, uv.Y),
        };

        var (nx, ny, nz) = FaceNormals[face];
        var (uAxis, vAxis) = FaceTangents[face];
        var blockPos = new Vector3(bx, by, bz);
        var corners = FaceCorners[face];

        int baseIndex = vertices.Count;
        Span<int> ao = stackalloc int[4];
        for (int i = 0; i < 4; i++)
        {
            // The three blocks diagonally adjacent to this vertex, one layer
            // out along the face normal, decide its ambient occlusion.
            int su = 2 * Component(corners[i], uAxis) - 1;
            int sv = 2 * Component(corners[i], vAxis) - 1;

            var side1 = Offset((bx + nx, by + ny, bz + nz), uAxis, su);
            var side2 = Offset((bx + nx, by + ny, bz + nz), vAxis, sv);
            var corner = Offset(side1, vAxis, sv);
            bool s1 = BlockInfo.IsSolid(sample(side1.X, side1.Y, side1.Z));
            bool s2 = BlockInfo.IsSolid(sample(side2.X, side2.Y, side2.Z));
            bool sc = BlockInfo.IsSolid(sample(corner.X, corner.Y, corner.Z));

            ao[i] = s1 && s2 ? 0 : 3 - ((s1 ? 1 : 0) + (s2 ? 1 : 0) + (sc ? 1 : 0));

            byte brightness = (byte)(255 * shade * AoFactor[ao[i]]);
            vertices.Add(new VertexPositionColorTexture(
                blockPos + corners[i], new Color(brightness, brightness, brightness), uvs[i]));
        }

        // Split the quad along the diagonal that connects the less-occluded
        // pair, otherwise AO gradients show a directional artifact.
        if (ao[0] + ao[2] >= ao[1] + ao[3])
        {
            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 3);
        }
        else
        {
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 3);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 3);
            indices.Add(baseIndex + 0);
        }
    }

    private static void AddWaterFace(List<VertexPositionColorTexture> vertices, List<int> indices,
        int bx, int by, int bz, int face)
    {
        byte shade = (byte)(255 * FaceShade[face]);
        var color = new Color(shade, shade, shade, WaterAlpha);
        var uv = TextureAtlas.GetUVBounds(BlockInfo.TileWater);
        var blockPos = new Vector3(bx, by, bz);
        var corners = FaceCorners[face];

        int baseIndex = vertices.Count;
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

    private static int Component(Vector3 v, int axis) => (int)(axis == 0 ? v.X : axis == 1 ? v.Y : v.Z);

    private static (int X, int Y, int Z) Offset((int X, int Y, int Z) p, int axis, int amount) => axis switch
    {
        0 => (p.X + amount, p.Y, p.Z),
        1 => (p.X, p.Y + amount, p.Z),
        _ => (p.X, p.Y, p.Z + amount),
    };
}
