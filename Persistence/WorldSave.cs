using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using MinecraftClone.World;

namespace MinecraftClone.Persistence;

public class InventorySlotData
{
    public int Slot { get; set; }
    public int Item { get; set; }
    public int Count { get; set; }
}

public class WorldMetadata
{
    public const int CurrentFormatVersion = 2; // v2 added Inventory

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public int Seed { get; set; }
    public float PlayerX { get; set; }
    public float PlayerY { get; set; }
    public float PlayerZ { get; set; }
    public float Yaw { get; set; }
    public float Pitch { get; set; }
    public int HotbarIndex { get; set; }
    public bool IsFlying { get; set; }

    /// <summary>0..1 day fraction; defaults to morning for saves that predate it.</summary>
    public float TimeOfDay { get; set; } = 0.1f;

    /// <summary>Null in pre-v2 saves — treated as an empty inventory.</summary>
    public List<InventorySlotData> Inventory { get; set; }
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

    /// <summary>Whether a world slot has been played (metadata on disk) —
    /// without creating its directories the way the constructor does.</summary>
    public static bool Exists(string worldName) =>
        File.Exists(Path.Combine("saves", worldName, "world.json"));

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
            {
                var meta = JsonSerializer.Deserialize<WorldMetadata>(File.ReadAllText(MetadataPath));
                // Older versions are additive (missing fields default); a file
                // from a *newer* game version can't be trusted to mean what we
                // think it means.
                if (meta != null && meta.FormatVersion <= WorldMetadata.CurrentFormatVersion)
                    return meta;
            }
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

    /// <summary>Erases the world from disk (all chunk files + metadata) — used
    /// when restarting with a new seed. The directories stay in place.</summary>
    public void DeleteAll()
    {
        try
        {
            if (Directory.Exists(_chunksDir))
                foreach (var file in Directory.GetFiles(_chunksDir))
                    File.Delete(file);
            if (File.Exists(MetadataPath))
                File.Delete(MetadataPath);
        }
        catch (Exception)
        {
            // Best effort: a stray locked file only means stale chunks linger
            // until they're overwritten.
        }
    }
}
