using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using MinecraftClone.World;

namespace MinecraftClone.Persistence;

public class WorldMetadata
{
    public int FormatVersion { get; set; } = 1;
    public int Seed { get; set; }
    public float PlayerX { get; set; }
    public float PlayerY { get; set; }
    public float PlayerZ { get; set; }
    public float Yaw { get; set; }
    public float Pitch { get; set; }
    public int HotbarIndex { get; set; }
    public bool IsFlying { get; set; }
}

/// <summary>
/// World persistence under saves\{world}\ next to the executable:
///   world.json          — seed, player state, hotbar
///   chunks\c_{x}_{z}.bin — one file per player-modified chunk: a small header
///                          (magic, version, coord) + gzip of the raw block array.
/// Unmodified chunks are never written — they regenerate from the seed.
/// TryLoadChunk/SaveChunk touch one file each with no shared state, so worker
/// threads can load different chunks concurrently.
/// </summary>
public class WorldSave
{
    private const uint Magic = 0x4B434D43; // "CMCK"
    private const int ChunkFormatVersion = 1;

    private readonly string _rootDir;
    private readonly string _chunksDir;

    private string MetadataPath => Path.Combine(_rootDir, "world.json");

    public WorldSave(string worldName = "default")
    {
        _rootDir = Path.Combine("saves", worldName);
        _chunksDir = Path.Combine(_rootDir, "chunks");
        Directory.CreateDirectory(_chunksDir);
    }

    private string ChunkPath(ChunkCoord coord) => Path.Combine(_chunksDir, $"c_{coord.X}_{coord.Z}.bin");

    /// <summary>Fills the chunk's blocks from disk. False = no (valid) save file; caller generates instead.</summary>
    public bool TryLoadChunk(Chunk chunk)
    {
        string path = ChunkPath(chunk.Coord);
        if (!File.Exists(path))
            return false;

        try
        {
            using var file = File.OpenRead(path);
            using var reader = new BinaryReader(file);
            if (reader.ReadUInt32() != Magic || reader.ReadInt32() != ChunkFormatVersion)
                return false;
            if (reader.ReadInt32() != chunk.Coord.X || reader.ReadInt32() != chunk.Coord.Z)
                return false;

            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            gzip.ReadExactly(chunk.Blocks);
            return true;
        }
        catch (Exception)
        {
            return false; // unreadable/corrupt file — regenerate from seed
        }
    }

    public void SaveChunk(Chunk chunk)
    {
        string path = ChunkPath(chunk.Coord);
        string tmp = path + ".tmp";

        using (var file = File.Create(tmp))
        using (var writer = new BinaryWriter(file))
        {
            writer.Write(Magic);
            writer.Write(ChunkFormatVersion);
            writer.Write(chunk.Coord.X);
            writer.Write(chunk.Coord.Z);
            using var gzip = new GZipStream(file, CompressionLevel.Fastest);
            gzip.Write(chunk.Blocks);
        }

        // Write-then-move so a crash mid-write never corrupts an existing save.
        File.Move(tmp, path, overwrite: true);
    }

    public WorldMetadata TryLoadMetadata()
    {
        try
        {
            if (File.Exists(MetadataPath))
                return JsonSerializer.Deserialize<WorldMetadata>(File.ReadAllText(MetadataPath));
        }
        catch (Exception)
        {
            // fall through — treat as a fresh world
        }
        return null;
    }

    public void SaveMetadata(WorldMetadata metadata)
    {
        File.WriteAllText(MetadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
    }
}
