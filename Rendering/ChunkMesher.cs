using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

/// <summary>CPU-side mesh: safe to build on a worker thread, uploaded to the GPU by ChunkMesh.</summary>
public record MeshData(
    TerrainVertex[] Vertices, int[] Indices,
    VertexPositionColorTexture[] WaterVertices, int[] WaterIndices,
    VertexPositionColorTexture[] CutoutVertices, int[] CutoutIndices,
    TerrainVertex[] LightVertices, int[] LightIndices)
{
    public bool IsEmpty => Indices.Length == 0 && WaterIndices.Length == 0
        && CutoutIndices.Length == 0 && LightIndices.Length == 0;
}

/// <summary>
/// Greedy meshing with per-vertex ambient occlusion: coplanar opaque faces
/// with the same tile merge into large quads when their AO and torch light
/// are uniform (flat interiors — the bulk of all faces); faces with corner
/// gradients stay 1x1 so lighting looks identical to the naive mesher. Merged
/// quads carry block-unit UVs that TerrainEffect wraps per block, so the
/// atlas tile repeats instead of stretching.
///
/// Water, plants, and glass keep their per-block special-case paths. Water
/// goes into a separate vertex list so the renderer can draw it in a
/// transparent pass after the opaque terrain. Vertices are in chunk-local
/// coordinates; ChunkMesh translates to world position. Pure CPU work — no
/// GraphicsDevice access, so it can run on worker threads. The outside
/// sampler must handle diagonal excursions (AO reads corner neighbors), i.e.
/// up to one chunk away in both X and Z at once.
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

    // Greedy sweep axes per face: A = the normal (slice) axis, P/Q = the two
    // in-plane axes. P is always the texture-u axis and Q the texture-v axis
    // (matches the UV assignment in FaceCorners/CornerLocalUV).
    private static readonly (int A, int P, int Q)[] FaceSliceAxes =
    {
        (1, 0, 2), (1, 0, 2), (2, 0, 1), (2, 0, 1), (0, 2, 1), (0, 2, 1),
    };

    // Tile-local UV of each FaceCorners corner: (0,1)=bottom-left of the tile,
    // (1,0)=top-right — the same orientation every face used pre-greedy.
    private static readonly int[] CornerCu = { 0, 1, 1, 0 };
    private static readonly int[] CornerCv = { 1, 1, 0, 0 };

    // Fake directional light: constant brightness per face orientation.
    private static readonly float[] FaceShade = { 1f, 0.5f, 0.8f, 0.8f, 0.65f, 0.65f };

    // Brightness per ambient-occlusion level (0 = fully occluded corner).
    private static readonly float[] AoFactor = { 0.55f, 0.7f, 0.85f, 1f };

    private const byte WaterAlpha = 160;

    /// <summary>One cell of the greedy mask. Kind classifies how the face's
    /// corner lighting varies, which decides the directions it may merge in
    /// while staying pixel-identical to per-block quads: constant lighting
    /// merges into 2D rects; lighting constant only along the texture-u axis
    /// (e.g. a wall band dark at the ground, light at the top) merges into
    /// u-strips; the v-symmetric case merges into v-strips.</summary>
    private struct MaskCell
    {
        public const byte None = 0, Uniform = 1, UConst = 2, VConst = 3;

        public byte Kind;
        public ushort Tile;
        public byte AoPacked;     // 4 corners x 2 bits
        public uint TorchPacked;  // 4 corners x 8 bits
    }

    private static bool SameCell(in MaskCell a, in MaskCell b) =>
        a.Kind == b.Kind && a.Tile == b.Tile
        && a.AoPacked == b.AoPacked && a.TorchPacked == b.TorchPacked;

    private static int AxisSize(int axis) =>
        axis == 0 ? Chunk.SizeX : axis == 1 ? Chunk.SizeY : Chunk.SizeZ;

    /// <param name="chunk">The chunk to mesh.</param>
    /// <param name="getOutsideBlock">
    /// Sampler for chunk-local coordinates outside this chunk's bounds — must
    /// resolve all 8 surrounding chunks (AO samples diagonals).
    /// </param>
    /// <param name="getOutsideLight">Block-light sampler with the same contract.</param>
    public static MeshData Build(Chunk chunk, Func<int, int, int, BlockType> getOutsideBlock,
        Func<int, int, int, byte> getOutsideLight)
    {
        var vertices = new List<TerrainVertex>();
        var indices = new List<int>();
        var waterVertices = new List<VertexPositionColorTexture>();
        var waterIndices = new List<int>();
        var cutoutVertices = new List<VertexPositionColorTexture>();
        var cutoutIndices = new List<int>();
        var lightVertices = new List<TerrainVertex>();
        var lightIndices = new List<int>();

        BlockType Sample(int x, int y, int z) =>
            Chunk.InBounds(x, y, z) ? chunk.GetBlock(x, y, z) : getOutsideBlock(x, y, z);
        byte SampleLight(int x, int y, int z) =>
            Chunk.InBounds(x, y, z) ? chunk.GetLight(x, y, z) : getOutsideLight(x, y, z);

        // Pass 1: the per-block special cases (plants, water, glass).
        for (int y = 0; y < Chunk.SizeY; y++)
        {
            for (int z = 0; z < Chunk.SizeZ; z++)
            {
                for (int x = 0; x < Chunk.SizeX; x++)
                {
                    var type = chunk.GetBlock(x, y, z);
                    if (type == BlockType.Air)
                        continue;

                    if (BlockInfo.IsPlant(type))
                    {
                        // Plants are crossed quads in the cutout pass — no
                        // faces, no culling, no AO, no neighbor reads.
                        AddCrossQuads(cutoutVertices, cutoutIndices, x, y, z, type, Color.White);
                        // Emitters glow: the same quads at full brightness in
                        // the max-blended light pass keep them bright at night.
                        if (BlockInfo.GetLightEmission(type) > 0)
                            AddCrossQuadsLight(lightVertices, lightIndices, x, y, z, type, Color.White);
                        continue;
                    }

                    if (BlockInfo.IsWater(type))
                    {
                        AddWaterBlock(waterVertices, waterIndices, x, y, z, type, Sample);
                        continue;
                    }

                    if (type == BlockType.Glass)
                    {
                        // See-through solid: faces go to the alpha-tested
                        // cutout pass; glass-vs-glass faces stay hidden so
                        // panes read as one sheet.
                        for (int face = 0; face < 6; face++)
                        {
                            var (nx, ny, nz) = FaceNormals[face];
                            var neighbor = Sample(x + nx, y + ny, z + nz);
                            if (!BlockInfo.IsOpaque(neighbor) && neighbor != BlockType.Glass)
                                AddGlassFace(cutoutVertices, cutoutIndices, lightVertices, lightIndices,
                                    x, y, z, face, type, Sample, SampleLight);
                        }
                    }
                }
            }
        }

        // Pass 2: greedy sweep over the ordinary opaque solids, one face
        // direction at a time, slice by slice along its normal.
        var mask = new MaskCell[Chunk.SizeX * Chunk.SizeY]; // sized for the largest slice
        Span<int> ao = stackalloc int[4];
        Span<byte> torch = stackalloc byte[4];

        for (int face = 0; face < 6; face++)
        {
            var (aAxis, pAxis, qAxis) = FaceSliceAxes[face];
            int sizeA = AxisSize(aAxis), sizeP = AxisSize(pAxis), sizeQ = AxisSize(qAxis);
            var (nx, ny, nz) = FaceNormals[face];

            for (int slice = 0; slice < sizeA; slice++)
            {
                bool any = false;
                for (int j = 0; j < sizeQ; j++)
                {
                    for (int i = 0; i < sizeP; i++)
                    {
                        mask[i + j * sizeP] = default;

                        int x = aAxis == 0 ? slice : pAxis == 0 ? i : j;
                        int y = aAxis == 1 ? slice : pAxis == 1 ? i : j;
                        int z = aAxis == 2 ? slice : pAxis == 2 ? i : j;

                        var type = chunk.GetBlock(x, y, z);
                        if (type == BlockType.Air || BlockInfo.IsPlant(type)
                            || BlockInfo.IsWater(type) || type == BlockType.Glass)
                            continue;
                        var neighbor = Sample(x + nx, y + ny, z + nz);
                        if (BlockInfo.IsOpaque(neighbor))
                            continue;

                        CornerLighting(x, y, z, face, Sample, SampleLight, ao, torch);
                        int tile = BlockInfo.GetFaceTile(type, (BlockFace)face);

                        // Corners: 0=BL, 1=BR, 2=TR, 3=TL in texture space.
                        bool uConst = ao[0] == ao[1] && ao[3] == ao[2]
                            && torch[0] == torch[1] && torch[3] == torch[2];
                        bool vConst = ao[0] == ao[3] && ao[1] == ao[2]
                            && torch[0] == torch[3] && torch[1] == torch[2];
                        byte kind = uConst && vConst ? MaskCell.Uniform
                            : uConst ? MaskCell.UConst
                            : vConst ? MaskCell.VConst
                            : MaskCell.None;

                        if (kind == MaskCell.None)
                        {
                            // Lighting varies in both directions — merging
                            // would visibly change the gradient. Emit as-is.
                            EmitFace(vertices, indices, lightVertices, lightIndices,
                                face, x, y, z, 1, 1, tile, ao, torch);
                        }
                        else
                        {
                            mask[i + j * sizeP] = new MaskCell
                            {
                                Kind = kind,
                                Tile = (ushort)tile,
                                AoPacked = (byte)(ao[0] | ao[1] << 2 | ao[2] << 4 | ao[3] << 6),
                                TorchPacked = (uint)(torch[0] | torch[1] << 8 | torch[2] << 16 | torch[3] << 24),
                            };
                            any = true;
                        }
                    }
                }
                if (!any)
                    continue;

                for (int j = 0; j < sizeQ; j++)
                {
                    for (int i = 0; i < sizeP; i++)
                    {
                        var cell = mask[i + j * sizeP];
                        if (cell.Kind == MaskCell.None)
                            continue;

                        // V-strips can't grow in u, u-strips can't grow in v —
                        // the direction the lighting varies in must stay one
                        // block wide for the gradient to survive merging.
                        int width = 1;
                        if (cell.Kind != MaskCell.VConst)
                            while (i + width < sizeP && SameCell(cell, mask[i + width + j * sizeP]))
                                width++;
                        int height = 1;
                        if (cell.Kind != MaskCell.UConst)
                            while (j + height < sizeQ && RowMatches(mask, cell, i, j + height, width, sizeP))
                                height++;
                        for (int jj = j; jj < j + height; jj++)
                            for (int ii = i; ii < i + width; ii++)
                                mask[ii + jj * sizeP].Kind = MaskCell.None;

                        int x = aAxis == 0 ? slice : pAxis == 0 ? i : j;
                        int y = aAxis == 1 ? slice : pAxis == 1 ? i : j;
                        int z = aAxis == 2 ? slice : pAxis == 2 ? i : j;

                        for (int c = 0; c < 4; c++)
                        {
                            ao[c] = (cell.AoPacked >> (2 * c)) & 3;
                            torch[c] = (byte)(cell.TorchPacked >> (8 * c));
                        }
                        EmitFace(vertices, indices, lightVertices, lightIndices,
                            face, x, y, z, width, height, cell.Tile, ao, torch);

                        i += width - 1;
                    }
                }
            }
        }

        return new MeshData(
            vertices.ToArray(), indices.ToArray(),
            waterVertices.ToArray(), waterIndices.ToArray(),
            cutoutVertices.ToArray(), cutoutIndices.ToArray(),
            lightVertices.ToArray(), lightIndices.ToArray());
    }

    private static bool RowMatches(MaskCell[] mask, in MaskCell cell, int i, int j, int width, int sizeP)
    {
        for (int ii = i; ii < i + width; ii++)
            if (!SameCell(cell, mask[ii + j * sizeP]))
                return false;
        return true;
    }

    /// <summary>Per-corner ambient occlusion and smooth torch light for one
    /// face — the three blocks diagonally adjacent to each vertex, one layer
    /// out along the face normal.</summary>
    private static void CornerLighting(int bx, int by, int bz, int face,
        Func<int, int, int, BlockType> sample, Func<int, int, int, byte> sampleLight,
        Span<int> ao, Span<byte> torch)
    {
        var (nx, ny, nz) = FaceNormals[face];
        var (uAxis, vAxis) = FaceTangents[face];
        var corners = FaceCorners[face];

        for (int i = 0; i < 4; i++)
        {
            int su = 2 * Component(corners[i], uAxis) - 1;
            int sv = 2 * Component(corners[i], vAxis) - 1;

            var front = (bx + nx, by + ny, bz + nz);
            var side1 = Offset(front, uAxis, su);
            var side2 = Offset(front, vAxis, sv);
            var corner = Offset(side1, vAxis, sv);
            bool s1 = BlockInfo.IsSolid(sample(side1.X, side1.Y, side1.Z));
            bool s2 = BlockInfo.IsSolid(sample(side2.X, side2.Y, side2.Z));
            bool sc = BlockInfo.IsSolid(sample(corner.X, corner.Y, corner.Z));

            ao[i] = s1 && s2 ? 0 : 3 - ((s1 ? 1 : 0) + (s2 ? 1 : 0) + (sc ? 1 : 0));

            // Smooth block light: the same four cells that decide AO decide
            // the vertex's torch light (solid cells hold 0, dimming corners).
            int light = sampleLight(front.Item1, front.Item2, front.Item3)
                + sampleLight(side1.X, side1.Y, side1.Z)
                + sampleLight(side2.X, side2.Y, side2.Z)
                + sampleLight(corner.X, corner.Y, corner.Z);
            torch[i] = (byte)(light / 4);
        }
    }

    /// <summary>Emits one terrain quad covering width x height blocks in the
    /// face's in-plane P/Q axes, into the opaque mesh and (when torch-lit)
    /// the max-blended light mesh.</summary>
    private static void EmitFace(List<TerrainVertex> vertices, List<int> indices,
        List<TerrainVertex> lightVertices, List<int> lightIndices,
        int face, int bx, int by, int bz, int width, int height, int tile,
        ReadOnlySpan<int> ao, ReadOnlySpan<byte> torch)
    {
        var (_, pAxis, qAxis) = FaceSliceAxes[face];
        Span<float> extent = stackalloc float[] { 1f, 1f, 1f };
        extent[pAxis] = width;
        extent[qAxis] = height;

        var uv = TextureAtlas.GetUVBounds(tile);
        var origin = new Vector2(uv.X, uv.Y);
        float shade = FaceShade[face];
        var corners = FaceCorners[face];
        var blockPos = new Vector3(bx, by, bz);

        int baseIndex = vertices.Count;
        int maxTorch = 0;
        Span<Vector3> positions = stackalloc Vector3[4];
        Span<Vector2> locals = stackalloc Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            positions[i] = blockPos + new Vector3(
                corners[i].X * extent[0], corners[i].Y * extent[1], corners[i].Z * extent[2]);
            locals[i] = new Vector2(CornerCu[i] * width, CornerCv[i] * height);
            maxTorch = Math.Max(maxTorch, torch[i]);

            byte brightness = (byte)(255 * shade * AoFactor[ao[i]]);
            vertices.Add(new TerrainVertex(positions[i],
                new Color(brightness, brightness, brightness), locals[i], origin));
        }

        // Split the quad along the diagonal that connects the less-occluded
        // pair, otherwise AO gradients show a directional artifact.
        Span<int> winding = ao[0] + ao[2] >= ao[1] + ao[3]
            ? stackalloc[] { 0, 1, 2, 0, 2, 3 }
            : stackalloc[] { 1, 2, 3, 1, 3, 0 };
        foreach (int offset in winding)
            indices.Add(baseIndex + offset);

        if (maxTorch == 0)
            return;

        // Duplicate the face into the light mesh: vertex color carries the
        // torch light level; the renderer max-blends it over the day-lit pass.
        int lightBase = lightVertices.Count;
        for (int i = 0; i < 4; i++)
        {
            byte brightness = (byte)(255 * (torch[i] / 15f) * AoFactor[ao[i]]);
            lightVertices.Add(new TerrainVertex(positions[i],
                new Color(brightness, brightness, brightness), locals[i], origin));
        }
        foreach (int offset in winding)
            lightIndices.Add(lightBase + offset);
    }

    // The two diagonal quads of a flower, inset from the block edges. Drawn
    // with CullNone, so a single winding per quad shows both sides.
    private static readonly Vector3[][] CrossQuads =
    {
        new[] { new Vector3(0.15f, 0, 0.15f), new Vector3(0.85f, 0, 0.85f), new Vector3(0.85f, 1, 0.85f), new Vector3(0.15f, 1, 0.15f) },
        new[] { new Vector3(0.85f, 0, 0.15f), new Vector3(0.15f, 0, 0.85f), new Vector3(0.15f, 1, 0.85f), new Vector3(0.85f, 1, 0.15f) },
    };

    private static void AddCrossQuads(List<VertexPositionColorTexture> vertices, List<int> indices, int bx, int by, int bz, BlockType type, Color color)
    {
        var uv = TextureAtlas.GetUVBounds(BlockInfo.GetFaceTile(type, BlockFace.South));
        var blockPos = new Vector3(bx, by, bz);

        foreach (var quad in CrossQuads)
        {
            int baseIndex = vertices.Count;
            vertices.Add(new VertexPositionColorTexture(blockPos + quad[0], color, new Vector2(uv.X, uv.W)));
            vertices.Add(new VertexPositionColorTexture(blockPos + quad[1], color, new Vector2(uv.Z, uv.W)));
            vertices.Add(new VertexPositionColorTexture(blockPos + quad[2], color, new Vector2(uv.Z, uv.Y)));
            vertices.Add(new VertexPositionColorTexture(blockPos + quad[3], color, new Vector2(uv.X, uv.Y)));

            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 3);
        }
    }

    /// <summary>Same crossed quads in TerrainVertex form for the light mesh
    /// (unit local UVs — a cross quad is never merged).</summary>
    private static void AddCrossQuadsLight(List<TerrainVertex> vertices, List<int> indices, int bx, int by, int bz, BlockType type, Color color)
    {
        var uv = TextureAtlas.GetUVBounds(BlockInfo.GetFaceTile(type, BlockFace.South));
        var origin = new Vector2(uv.X, uv.Y);
        var blockPos = new Vector3(bx, by, bz);

        foreach (var quad in CrossQuads)
        {
            int baseIndex = vertices.Count;
            for (int i = 0; i < 4; i++)
                vertices.Add(new TerrainVertex(blockPos + quad[i], color,
                    new Vector2(CornerCu[i], CornerCv[i]), origin));

            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 3);
        }
    }

    /// <summary>Glass faces: alpha-tested cutout quads (absolute atlas UVs),
    /// with a TerrainVertex duplicate in the light mesh when torch-lit.</summary>
    private static void AddGlassFace(List<VertexPositionColorTexture> vertices, List<int> indices,
        List<TerrainVertex> lightVertices, List<int> lightIndices,
        int bx, int by, int bz, int face, BlockType type,
        Func<int, int, int, BlockType> sample, Func<int, int, int, byte> sampleLight)
    {
        float shade = FaceShade[face];
        var uv = TextureAtlas.GetUVBounds(BlockInfo.GetFaceTile(type, (BlockFace)face));
        Span<Vector2> uvs = stackalloc Vector2[]
        {
            new(uv.X, uv.W), new(uv.Z, uv.W), new(uv.Z, uv.Y), new(uv.X, uv.Y),
        };

        Span<int> ao = stackalloc int[4];
        Span<byte> torch = stackalloc byte[4];
        CornerLighting(bx, by, bz, face, sample, sampleLight, ao, torch);

        var blockPos = new Vector3(bx, by, bz);
        var corners = FaceCorners[face];
        int baseIndex = vertices.Count;
        int maxTorch = 0;
        for (int i = 0; i < 4; i++)
        {
            maxTorch = Math.Max(maxTorch, torch[i]);
            byte brightness = (byte)(255 * shade * AoFactor[ao[i]]);
            vertices.Add(new VertexPositionColorTexture(
                blockPos + corners[i], new Color(brightness, brightness, brightness), uvs[i]));
        }

        Span<int> winding = ao[0] + ao[2] >= ao[1] + ao[3]
            ? stackalloc[] { 0, 1, 2, 0, 2, 3 }
            : stackalloc[] { 1, 2, 3, 1, 3, 0 };
        foreach (int offset in winding)
            indices.Add(baseIndex + offset);

        if (maxTorch == 0)
            return;

        var origin = new Vector2(uv.X, uv.Y);
        int lightBase = lightVertices.Count;
        for (int i = 0; i < 4; i++)
        {
            byte brightness = (byte)(255 * (torch[i] / 15f) * AoFactor[ao[i]]);
            lightVertices.Add(new TerrainVertex(blockPos + corners[i],
                new Color(brightness, brightness, brightness),
                new Vector2(CornerCu[i], CornerCv[i]), origin));
        }
        foreach (int offset in winding)
            lightIndices.Add(lightBase + offset);
    }

    /// <summary>
    /// Rendered surface height: full cube for falling water and any cell with
    /// water above (a waterfall is a continuous sheet); otherwise proportional
    /// to the flow level, with the classic 14/16 sea-surface look for sources.
    /// </summary>
    private static float WaterHeight(BlockType type, BlockType above) =>
        BlockInfo.IsWater(above) || type == BlockType.WaterFall
            ? 1f
            : BlockInfo.GetWaterLevel(type) * (0.875f / 8f);

    private static void AddWaterBlock(List<VertexPositionColorTexture> vertices, List<int> indices,
        int bx, int by, int bz, BlockType type, Func<int, int, int, BlockType> sample)
    {
        var above = sample(bx, by + 1, bz);
        float myHeight = WaterHeight(type, above);

        for (int face = 0; face < 6; face++)
        {
            var (nx, ny, nz) = FaceNormals[face];
            var neighbor = sample(bx + nx, by + ny, bz + nz);
            if (BlockInfo.IsOpaque(neighbor))
                continue; // hidden, as for opaque blocks

            if (face is (int)BlockFace.Top or (int)BlockFace.Bottom)
            {
                // Water above/below makes the face interior.
                if (!BlockInfo.IsWater(neighbor))
                    AddWaterFace(vertices, indices, bx, by, bz, face, 0f, myHeight);
                continue;
            }

            // Sides: against air, a full band up to our surface; against lower
            // water, just the exposed band between the two surface heights.
            float neighborHeight = BlockInfo.IsWater(neighbor)
                ? WaterHeight(neighbor, sample(bx + nx, by + 1, bz + nz))
                : 0f;
            if (myHeight > neighborHeight)
                AddWaterFace(vertices, indices, bx, by, bz, face, neighborHeight, myHeight);
        }
    }

    private static void AddWaterFace(List<VertexPositionColorTexture> vertices, List<int> indices,
        int bx, int by, int bz, int face, float yBottom, float yTop)
    {
        byte shade = (byte)(255 * FaceShade[face]);
        var color = new Color(shade, shade, shade, WaterAlpha);
        var uv = TextureAtlas.GetUVBounds(BlockInfo.TileWater);
        var blockPos = new Vector3(bx, by, bz);
        var corners = FaceCorners[face];
        bool isSide = face >= 2;

        Span<Vector2> uvs = stackalloc Vector2[]
        {
            new(uv.X, uv.W), new(uv.Z, uv.W), new(uv.Z, uv.Y), new(uv.X, uv.Y),
        };

        int baseIndex = vertices.Count;
        for (int i = 0; i < 4; i++)
        {
            var corner = corners[i];
            float h = corner.Y < 0.5f ? yBottom : yTop;
            var texCoord = uvs[i];
            if (isSide) // texture anchored at the block bottom, so bands don't stretch
                texCoord.Y = MathHelper.Lerp(uv.W, uv.Y, h);
            vertices.Add(new VertexPositionColorTexture(
                new Vector3(blockPos.X + corner.X, blockPos.Y + h, blockPos.Z + corner.Z), color, texCoord));
        }

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
