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
    public const int LoadRadius = 12;                       // chunks that get meshes
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

    /// <summary>Total mesh vertices across all passes — perf telemetry.</summary>
    public int TotalVertexCount
    {
        get
        {
            int total = 0;
            foreach (var mesh in _meshes.Values)
                total += mesh.VertexCount;
            return total;
        }
    }

    /// <summary>Tallies block-type occurrences across every loaded chunk into
    /// <paramref name="counts"/> (indexed by block id; must be length 256) and
    /// returns the number of chunks sampled. Debug-only — walks raw storage.</summary>
    public int CountBlocks(long[] counts)
    {
        System.Array.Clear(counts, 0, counts.Length);
        foreach (var chunk in _chunks.Values)
        {
            var blocks = chunk.Blocks;
            for (int i = 0; i < blocks.Length; i++)
                counts[blocks[i]]++;
        }
        return _chunks.Count;
    }

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
        var oldType = chunk.GetBlock(localX, y, localZ);
        chunk.SetBlock(localX, y, localZ, type);
        chunk.IsModified = true;
        chunk.Version++;

        UpdateLightForEdit(chunk, x, y, z, oldType, type);
        chunk.RecomputeSkyColumn(localX, localZ);
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
        var oldType = chunk.GetBlock(localX, y, localZ);
        chunk.SetBlock(localX, y, localZ, type);
        chunk.IsModified = true;
        chunk.Version++; // any in-flight mesh build is now stale
        chunk.MeshDirty = true;

        UpdateLightForEdit(chunk, x, y, z, oldType, type);
        chunk.RecomputeSkyColumn(localX, localZ);

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

        var data = ChunkMesher.Build(chunk, neighbors.Sample, neighbors.SampleLight, neighbors.SampleSkyLight);
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
            SeedLightAround(chunk.Coord);
        }
    }

    // --- Block light -------------------------------------------------------
    // Levels 0-15 per cell, BFS-flooded from emitters. Light is derived state:
    // never saved, reseeded from the cached per-chunk emitter lists whenever a
    // chunk (re)enters the loaded set, so torch light crosses chunk borders no
    // matter which side loads first. AddLight is monotonic (only raises), so
    // reseeding is idempotent and cheap.

    private readonly Queue<(int X, int Y, int Z)> _lightSpread = new();
    private readonly Queue<(int X, int Y, int Z, byte Level)> _lightRemove = new();

    public byte GetLight(int x, int y, int z)
    {
        if (y < 0 || y >= Chunk.SizeY)
            return 0;
        return _chunks.TryGetValue(new ChunkCoord(x >> 4, z >> 4), out var chunk)
            ? chunk.GetLight(x & 15, y, z & 15)
            : (byte)0;
    }

    private void SetLight(int x, int y, int z, byte level)
    {
        var coord = new ChunkCoord(x >> 4, z >> 4);
        if (!_chunks.TryGetValue(coord, out var chunk))
            return;
        int localX = x & 15, localZ = z & 15;
        chunk.SetLight(localX, y, localZ, level);
        chunk.Version++; // in-flight mesh builds read stale light now
        chunk.MeshDirty = true;
        // Border light changes the neighbor's vertex light sampling too.
        if (localX == 0) MarkMeshDirty(new ChunkCoord(coord.X - 1, coord.Z));
        if (localX == Chunk.SizeX - 1) MarkMeshDirty(new ChunkCoord(coord.X + 1, coord.Z));
        if (localZ == 0) MarkMeshDirty(new ChunkCoord(coord.X, coord.Z - 1));
        if (localZ == Chunk.SizeZ - 1) MarkMeshDirty(new ChunkCoord(coord.X, coord.Z + 1));
    }

    private void UpdateLightForEdit(Chunk chunk, int x, int y, int z, BlockType oldType, BlockType newType)
    {
        var local = ((byte)(x & 15), (byte)y, (byte)(z & 15));
        if (BlockInfo.GetLightEmission(oldType) > 0)
        {
            chunk.Emitters.Remove(local);
            RemoveLight(x, y, z);
        }
        // A solid block dropped into a lit cell blocks the light path.
        if (BlockInfo.IsSolid(newType) && GetLight(x, y, z) > 0)
            RemoveLight(x, y, z);

        byte emission = BlockInfo.GetLightEmission(newType);
        if (emission > 0)
        {
            chunk.Emitters.Add(local);
            AddLight(x, y, z, emission);
        }
        else if (!BlockInfo.IsSolid(newType))
        {
            // An opened cell inherits from its brightest neighbor.
            int best = Math.Max(
                Math.Max(Math.Max(GetLight(x + 1, y, z), GetLight(x - 1, y, z)),
                         Math.Max(GetLight(x, y, z + 1), GetLight(x, y, z - 1))),
                Math.Max(GetLight(x, y + 1, z), GetLight(x, y - 1, z)));
            if (best > 1)
                AddLight(x, y, z, (byte)(best - 1));
        }
    }

    private void AddLight(int x, int y, int z, byte level)
    {
        if (GetLight(x, y, z) >= level)
            return;
        SetLight(x, y, z, level);
        _lightSpread.Enqueue((x, y, z));
        SpreadPendingLight();
    }

    private void RemoveLight(int x, int y, int z)
    {
        byte old = GetLight(x, y, z);
        if (old == 0)
            return;
        SetLight(x, y, z, 0);
        _lightRemove.Enqueue((x, y, z, old));

        while (_lightRemove.TryDequeue(out var cell))
        {
            Span<(int X, int Y, int Z)> neighbors = stackalloc[]
            {
                (cell.X + 1, cell.Y, cell.Z), (cell.X - 1, cell.Y, cell.Z),
                (cell.X, cell.Y + 1, cell.Z), (cell.X, cell.Y - 1, cell.Z),
                (cell.X, cell.Y, cell.Z + 1), (cell.X, cell.Y, cell.Z - 1),
            };
            foreach (var (nx, ny, nz) in neighbors)
            {
                byte neighborLight = GetLight(nx, ny, nz);
                if (neighborLight == 0)
                    continue;
                if (neighborLight < cell.Level)
                {
                    // This cell was lit through the removed path — unlight it
                    // and keep walking outward.
                    SetLight(nx, ny, nz, 0);
                    _lightRemove.Enqueue((nx, ny, nz, neighborLight));
                }
                else
                {
                    // Independent light survives at the boundary; respread it
                    // into the darkness we just created.
                    _lightSpread.Enqueue((nx, ny, nz));
                }
            }
        }
        SpreadPendingLight();
    }

    private void SpreadPendingLight()
    {
        while (_lightSpread.TryDequeue(out var cell))
        {
            byte level = GetLight(cell.X, cell.Y, cell.Z);
            if (level <= 1)
                continue;
            Span<(int X, int Y, int Z)> neighbors = stackalloc[]
            {
                (cell.X + 1, cell.Y, cell.Z), (cell.X - 1, cell.Y, cell.Z),
                (cell.X, cell.Y + 1, cell.Z), (cell.X, cell.Y - 1, cell.Z),
                (cell.X, cell.Y, cell.Z + 1), (cell.X, cell.Y, cell.Z - 1),
            };
            foreach (var (nx, ny, nz) in neighbors)
            {
                if (ny < 0 || ny >= Chunk.SizeY || BlockInfo.IsSolid(GetBlock(nx, ny, nz)))
                    continue;
                if (GetLight(nx, ny, nz) < level - 1)
                {
                    SetLight(nx, ny, nz, (byte)(level - 1));
                    _lightSpread.Enqueue((nx, ny, nz));
                }
            }
        }
    }

    /// <summary>Repropagates every cached emitter in the 3x3 chunk neighborhood —
    /// called when a chunk integrates so light crosses into it from all sides.</summary>
    private void SeedLightAround(ChunkCoord center)
    {
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (!_chunks.TryGetValue(new ChunkCoord(center.X + dx, center.Z + dz), out var chunk))
                    continue;
                foreach (var (lx, ly, lz) in chunk.Emitters)
                {
                    byte emission = BlockInfo.GetLightEmission(chunk.GetBlock(lx, ly, lz));
                    if (emission > 0)
                        AddLight(chunk.Coord.X * Chunk.SizeX + lx, ly, chunk.Coord.Z * Chunk.SizeZ + lz, emission);
                }
            }
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
                    if (_save.TryLoadChunk(chunk))
                        ScanEmitters(chunk); // saved chunks may contain torches
                    else
                        _generator.Generate(chunk);
                    chunk.ComputeSkyLight(); // derived; off the main thread here
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
                    var data = ChunkMesher.Build(loaded, neighbors.Sample, neighbors.SampleLight, neighbors.SampleSkyLight);
                    _meshResults.Enqueue((loaded.Coord, version, data));
                });
            }
        }
    }

    private static void ScanEmitters(Chunk chunk)
    {
        for (int y = 0; y < Chunk.SizeY; y++)
            for (int z = 0; z < Chunk.SizeZ; z++)
                for (int x = 0; x < Chunk.SizeX; x++)
                    if (BlockInfo.GetLightEmission(chunk.GetBlock(x, y, z)) > 0)
                        chunk.Emitters.Add(((byte)x, (byte)y, (byte)z));
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

        public byte SampleLight(int x, int y, int z)
        {
            if (y < 0 || y >= Chunk.SizeY)
                return 0;
            int gridX = (x + Chunk.SizeX) >> 4;
            int gridZ = (z + Chunk.SizeZ) >> 4;
            return _grid[gridX + gridZ * 3].GetLight(x & 15, y, z & 15);
        }

        public byte SampleSkyLight(int x, int y, int z)
        {
            if (y < 0) return 0;
            if (y >= Chunk.SizeY) return 15; // open sky above the world top
            int gridX = (x + Chunk.SizeX) >> 4;
            int gridZ = (z + Chunk.SizeZ) >> 4;
            return _grid[gridX + gridZ * 3].GetSkyLight(x & 15, y, z & 15);
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
