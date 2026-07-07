using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.Persistence;
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
    private const int GenerateRadius = LoadRadius + 2;      // meshing needs all 8 neighbors (AO reads diagonals)
    private const int UnloadRadius = LoadRadius + 3;        // beyond this, chunks are dropped
    private const int MeshUploadsPerFrame = 4;              // GPU buffer creations per frame, to avoid hitches
    private const int GenerateIntegrationsPerFrame = 64;    // cheap dictionary adds, higher budget

    private readonly GraphicsDevice _device;
    private readonly TerrainGenerator _generator;
    private readonly WorldSave _save;

    private readonly Dictionary<ChunkCoord, Chunk> _chunks = new();
    private readonly Dictionary<ChunkCoord, ChunkMesh> _meshes = new();
    private readonly HashSet<ChunkCoord> _generating = new();
    private readonly HashSet<ChunkCoord> _meshing = new();
    private readonly ConcurrentQueue<Chunk> _generatedChunks = new();
    private readonly ConcurrentQueue<(ChunkCoord Coord, int Version, MeshData Data)> _meshResults = new();

    // All offsets within the generate radius, nearest first, so the terrain
    // around the player streams in before the horizon does.
    private static readonly (int X, int Z)[] SortedOffsets = BuildSortedOffsets();

    public IEnumerable<ChunkMesh> Meshes => _meshes.Values;
    public int LoadedChunkCount => _chunks.Count;
    public int PendingCount => _generating.Count + _meshing.Count;

    public ChunkManager(GraphicsDevice device, TerrainGenerator generator, WorldSave save)
    {
        _device = device;
        _generator = generator;
        _save = save;
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

    /// <summary>
    /// Player edit: sets the block and rebuilds the affected mesh(es) immediately
    /// on the main thread (&lt;2 ms for a chunk) so breaking/placing feels instant.
    /// </summary>
    public void SetBlock(int x, int y, int z, BlockType type)
    {
        if (y < 0 || y >= Chunk.SizeY)
            return;
        var coord = new ChunkCoord(x >> 4, z >> 4);
        if (!_chunks.TryGetValue(coord, out var chunk))
            return;

        int localX = x & 15, localZ = z & 15;
        chunk.SetBlock(localX, y, localZ, type);
        chunk.IsModified = true;
        chunk.Version++;

        RemeshNow(coord);
        // A border edit changes the neighbor's face culling too.
        if (localX == 0) RemeshNow(new ChunkCoord(coord.X - 1, coord.Z));
        if (localX == Chunk.SizeX - 1) RemeshNow(new ChunkCoord(coord.X + 1, coord.Z));
        if (localZ == 0) RemeshNow(new ChunkCoord(coord.X, coord.Z - 1));
        if (localZ == Chunk.SizeZ - 1) RemeshNow(new ChunkCoord(coord.X, coord.Z + 1));
    }

    /// <summary>
    /// Simulation edit (water flow): sets the block and marks meshes dirty for
    /// the async mesher instead of remeshing synchronously — many cells change
    /// per tick and the MeshDirty flag coalesces them into one rebuild per chunk.
    /// Returns false when the chunk isn't loaded so the simulation can stop
    /// instead of retrying forever at the streaming horizon.
    /// </summary>
    public bool SetBlockDeferred(int x, int y, int z, BlockType type)
    {
        if (y < 0 || y >= Chunk.SizeY)
            return false;
        var coord = new ChunkCoord(x >> 4, z >> 4);
        if (!_chunks.TryGetValue(coord, out var chunk))
            return false;

        int localX = x & 15, localZ = z & 15;
        chunk.SetBlock(localX, y, localZ, type);
        chunk.IsModified = true;
        chunk.Version++; // any in-flight mesh build is now stale
        chunk.MeshDirty = true;

        // A border edit changes the neighbor's face culling too.
        if (localX == 0) MarkMeshDirty(new ChunkCoord(coord.X - 1, coord.Z));
        if (localX == Chunk.SizeX - 1) MarkMeshDirty(new ChunkCoord(coord.X + 1, coord.Z));
        if (localZ == 0) MarkMeshDirty(new ChunkCoord(coord.X, coord.Z - 1));
        if (localZ == Chunk.SizeZ - 1) MarkMeshDirty(new ChunkCoord(coord.X, coord.Z + 1));
        return true;
    }

    private void MarkMeshDirty(ChunkCoord coord)
    {
        if (_chunks.TryGetValue(coord, out var chunk))
            chunk.MeshDirty = true;
    }

    private void RemeshNow(ChunkCoord coord)
    {
        if (!_chunks.TryGetValue(coord, out var chunk))
            return;
        if (!TryGetNeighbors(coord, out var neighbors))
        {
            chunk.MeshDirty = true; // the async path picks it up once neighbors exist
            return;
        }

        var data = ChunkMesher.Build(chunk, neighbors.Sample);
        if (_meshes.Remove(coord, out var oldMesh))
            oldMesh.Dispose();
        _meshes[coord] = new ChunkMesh(_device, coord, data);
        chunk.MeshDirty = false;
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
            if (!_chunks.TryGetValue(result.Coord, out var chunk))
                continue; // chunk was unloaded while its mesh was being built
            if (chunk.Version != result.Version)
                continue; // edited while meshing — a newer synchronous mesh exists

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
                    // Player-modified chunks come from disk; everything else
                    // regenerates deterministically from the seed.
                    if (!_save.TryLoadChunk(chunk))
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
                int version = loaded.Version;
                Task.Run(() =>
                {
                    var data = ChunkMesher.Build(loaded, neighbors.Sample);
                    _meshResults.Enqueue((loaded.Coord, version, data));
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
            if (_chunks.Remove(coord, out var chunk) && chunk.IsModified)
                _save.SaveChunk(chunk);
            if (_meshes.Remove(coord, out var mesh))
                mesh.Dispose();
        }
    }

    /// <summary>Writes every loaded chunk the player has edited. Used by F5 and on exit.</summary>
    public void SaveAllModified()
    {
        foreach (var chunk in _chunks.Values)
        {
            if (!chunk.IsModified)
                continue;
            _save.SaveChunk(chunk);
            chunk.IsModified = false; // already on disk; only re-save after another edit
        }
    }

    private bool TryGetNeighbors(ChunkCoord coord, out NeighborChunks neighbors)
    {
        var grid = new Chunk[9];
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (!_chunks.TryGetValue(new ChunkCoord(coord.X + dx, coord.Z + dz), out var chunk))
                {
                    neighbors = default;
                    return false;
                }
                grid[dx + 1 + (dz + 1) * 3] = chunk;
            }
        }
        neighbors = new NeighborChunks(grid);
        return true;
    }

    /// <summary>
    /// The 3x3 grid of chunks around (and including) the one being meshed,
    /// captured at schedule time so mesher workers never read the
    /// (main-thread-owned) chunk dictionary. Ambient occlusion samples diagonal
    /// neighbors, so all 8 surrounding chunks are required.
    /// </summary>
    private readonly struct NeighborChunks
    {
        private readonly Chunk[] _grid;

        public NeighborChunks(Chunk[] grid) => _grid = grid;

        // Sampler for chunk-local coordinates up to one chunk outside the
        // meshed chunk in X and/or Z.
        public BlockType Sample(int x, int y, int z)
        {
            if (y < 0 || y >= Chunk.SizeY)
                return BlockType.Air;
            int gridX = (x + Chunk.SizeX) >> 4;
            int gridZ = (z + Chunk.SizeZ) >> 4;
            return _grid[gridX + gridZ * 3].GetBlock(x & 15, y, z & 15);
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
