using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.Rendering;

namespace MinecraftClone.World;

/// <summary>
/// The world: owns all loaded chunks and their meshes, streams them in and out
/// around the player, and is the single world-coordinate block API
/// (GetBlock/SetBlock) used by meshing, physics, and raycasting.
///
/// Threading model: workers generate block data and build mesh vertex arrays;
/// the main thread alone mutates the chunk/mesh dictionaries and touches the
/// GraphicsDevice. Workers hand results back through concurrent queues.
/// </summary>
public class ChunkManager : IDisposable
{
    public const int LoadRadius = 8;                        // chunks that get meshes
    private const int GenerateRadius = LoadRadius + 1;      // chunks with block data (border culling needs +1)
    private const int UnloadRadius = LoadRadius + 3;        // beyond this, chunks are dropped
    private const int MeshUploadsPerFrame = 4;              // GPU buffer creations per frame, to avoid hitches
    private const int GenerateIntegrationsPerFrame = 64;    // cheap dictionary adds, higher budget

    private readonly GraphicsDevice _device;
    private readonly TerrainGenerator _generator;

    private readonly Dictionary<ChunkCoord, Chunk> _chunks = new();
    private readonly Dictionary<ChunkCoord, ChunkMesh> _meshes = new();
    private readonly HashSet<ChunkCoord> _generating = new();
    private readonly HashSet<ChunkCoord> _meshing = new();
    private readonly ConcurrentQueue<Chunk> _generatedChunks = new();
    private readonly ConcurrentQueue<(ChunkCoord Coord, MeshData Data)> _meshResults = new();

    // All offsets within the generate radius, nearest first, so the terrain
    // around the player streams in before the horizon does.
    private static readonly (int X, int Z)[] SortedOffsets = BuildSortedOffsets();

    public IEnumerable<ChunkMesh> Meshes => _meshes.Values;
    public int LoadedChunkCount => _chunks.Count;
    public int PendingCount => _generating.Count + _meshing.Count;

    public ChunkManager(GraphicsDevice device, TerrainGenerator generator)
    {
        _device = device;
        _generator = generator;
    }

    public void Update(Vector3 playerPosition)
    {
        var center = ToChunkCoord(playerPosition);
        IntegrateGeneratedChunks();
        UploadFinishedMeshes();
        ScheduleWork(center);
        UnloadDistantChunks(center);
    }

    public static ChunkCoord ToChunkCoord(Vector3 position) =>
        new((int)MathF.Floor(position.X) >> 4, (int)MathF.Floor(position.Z) >> 4);

    public bool IsChunkLoaded(ChunkCoord coord) => _chunks.ContainsKey(coord);

    public BlockType GetBlock(int x, int y, int z)
    {
        if (y < 0 || y >= Chunk.SizeY)
            return BlockType.Air;
        // Arithmetic shift floors correctly for negative coordinates (chunk size 16).
        if (!_chunks.TryGetValue(new ChunkCoord(x >> 4, z >> 4), out var chunk))
            return BlockType.Air;
        return chunk.GetBlock(x & 15, y, z & 15);
    }

    private void IntegrateGeneratedChunks()
    {
        int budget = GenerateIntegrationsPerFrame;
        while (budget-- > 0 && _generatedChunks.TryDequeue(out var chunk))
        {
            _generating.Remove(chunk.Coord);
            _chunks[chunk.Coord] = chunk;
        }
    }

    private void UploadFinishedMeshes()
    {
        int budget = MeshUploadsPerFrame;
        while (budget-- > 0 && _meshResults.TryDequeue(out var result))
        {
            _meshing.Remove(result.Coord);
            if (!_chunks.ContainsKey(result.Coord))
                continue; // chunk was unloaded while its mesh was being built

            if (_meshes.Remove(result.Coord, out var oldMesh))
                oldMesh.Dispose();
            _meshes[result.Coord] = new ChunkMesh(_device, result.Coord, result.Data);
        }
    }

    private void ScheduleWork(ChunkCoord center)
    {
        foreach (var (dx, dz) in SortedOffsets)
        {
            int distSq = dx * dx + dz * dz;
            var coord = new ChunkCoord(center.X + dx, center.Z + dz);

            if (!_chunks.ContainsKey(coord) && !_generating.Contains(coord))
            {
                _generating.Add(coord);
                var chunk = new Chunk(coord);
                Task.Run(() =>
                {
                    _generator.Generate(chunk);
                    _generatedChunks.Enqueue(chunk);
                });
            }

            if (distSq <= LoadRadius * LoadRadius
                && _chunks.TryGetValue(coord, out var loaded)
                && (loaded.MeshDirty || !_meshes.ContainsKey(coord))
                && !_meshing.Contains(coord)
                && TryGetNeighbors(coord, out var neighbors))
            {
                _meshing.Add(coord);
                loaded.MeshDirty = false;
                Task.Run(() =>
                {
                    var data = ChunkMesher.Build(loaded, neighbors.Sample);
                    _meshResults.Enqueue((loaded.Coord, data));
                });
            }
        }
    }

    private void UnloadDistantChunks(ChunkCoord center)
    {
        List<ChunkCoord> toUnload = null;
        foreach (var coord in _chunks.Keys)
        {
            int dx = coord.X - center.X, dz = coord.Z - center.Z;
            if (dx * dx + dz * dz > UnloadRadius * UnloadRadius)
                (toUnload ??= new List<ChunkCoord>()).Add(coord);
        }
        if (toUnload == null)
            return;

        foreach (var coord in toUnload)
        {
            _chunks.Remove(coord);
            if (_meshes.Remove(coord, out var mesh))
                mesh.Dispose();
        }
    }

    private bool TryGetNeighbors(ChunkCoord coord, out NeighborChunks neighbors)
    {
        if (_chunks.TryGetValue(new ChunkCoord(coord.X, coord.Z - 1), out var north)
            && _chunks.TryGetValue(new ChunkCoord(coord.X, coord.Z + 1), out var south)
            && _chunks.TryGetValue(new ChunkCoord(coord.X + 1, coord.Z), out var east)
            && _chunks.TryGetValue(new ChunkCoord(coord.X - 1, coord.Z), out var west))
        {
            neighbors = new NeighborChunks(north, south, east, west);
            return true;
        }
        neighbors = default;
        return false;
    }

    /// <summary>
    /// The four face neighbors of a chunk, captured at schedule time so mesher
    /// workers never read the (main-thread-owned) chunk dictionary.
    /// </summary>
    private readonly struct NeighborChunks
    {
        private readonly Chunk _north, _south, _east, _west;

        public NeighborChunks(Chunk north, Chunk south, Chunk east, Chunk west)
        {
            _north = north;
            _south = south;
            _east = east;
            _west = west;
        }

        // Sampler for chunk-local coordinates just outside the meshed chunk.
        // Face-neighbor lookups only ever leave the chunk along one axis.
        public BlockType Sample(int x, int y, int z)
        {
            if (y < 0 || y >= Chunk.SizeY) return BlockType.Air;
            if (x < 0) return _west.GetBlock(x + Chunk.SizeX, y, z);
            if (x >= Chunk.SizeX) return _east.GetBlock(x - Chunk.SizeX, y, z);
            if (z < 0) return _north.GetBlock(x, y, z + Chunk.SizeZ);
            if (z >= Chunk.SizeZ) return _south.GetBlock(x, y, z - Chunk.SizeZ);
            return BlockType.Air;
        }
    }

    private static (int, int)[] BuildSortedOffsets()
    {
        var offsets = new List<(int X, int Z)>();
        for (int dx = -GenerateRadius; dx <= GenerateRadius; dx++)
            for (int dz = -GenerateRadius; dz <= GenerateRadius; dz++)
                if (dx * dx + dz * dz <= GenerateRadius * GenerateRadius)
                    offsets.Add((dx, dz));
        offsets.Sort((a, b) => (a.X * a.X + a.Z * a.Z).CompareTo(b.X * b.X + b.Z * b.Z));
        return offsets.ToArray();
    }

    public void Dispose()
    {
        foreach (var mesh in _meshes.Values)
            mesh.Dispose();
        _meshes.Clear();
        _chunks.Clear();
    }
}
