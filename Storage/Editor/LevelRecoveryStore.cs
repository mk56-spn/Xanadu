using Xanadu.Serialisation.Levels;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xanadu.Core.Diagnostics;
using Xanadu.Core.Storage;
using Xanadu.Model.Levels;

namespace Xanadu.Storage.Editor;

/// <summary>
///     Autosaved copies of levels with unsaved edits, one file per level, kept
///     away from the level files themselves so a recovery can never overwrite
///     real work. An untitled level has a single shared slot.
/// </summary>
public sealed class LevelRecoveryStore(string directory)
{
    public const string UserFolder = "user://recovery";

    private const string Format = "XMR";
    private const ushort MajorVersion = 1;
    private const ushort MinorVersion = 0;

    private static readonly byte[] Magic = "XMR1"u8.ToArray();

    private static LevelRecoveryStore? _default;

    public static LevelRecoveryStore Default =>
        _default ??= new LevelRecoveryStore(GamePaths.Full(UserFolder));

    public string Directory { get; } = directory;

    public void Write(string levelPath, LevelDocument level, DateTimeOffset now)
    {
        System.IO.Directory.CreateDirectory(Directory);

        byte[] meta;
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(levelPath);
            writer.Write(now.UtcTicks);
            writer.Flush();
            meta = stream.ToArray();
        }

        var file = new ChunkWriter(Magic, MajorVersion, MinorVersion);
        file.Add((uint)ChunkId.Meta, meta);
        file.Add((uint)ChunkId.Map, XmfBinary.Write(level));
        BinaryFile.Save(PathFor(levelPath), file.ToArray());
    }

    /// <summary>The autosave for a level, or null if there is none or it cannot be read.</summary>
    public Entry? Read(string levelPath)
    {
        string path = PathFor(levelPath);
        if (!File.Exists(path))
            return null;

        try
        {
            var file = ChunkReader.Open(File.ReadAllBytes(path), Magic, MajorVersion, Format);

            using BinaryReader meta = file.Get((uint)ChunkId.Meta, Format).OpenReader();
            return new Entry
            {
                LevelPath = meta.ReadString(),
                SavedAt = new DateTimeOffset(meta.ReadInt64(), TimeSpan.Zero),
                Level = LevelSerializer.Deserialize(file.Get((uint)ChunkId.Map, Format).ToArray())
            };
        }
        catch (Exception exception) when (exception is IOException or FormatException or XmfFormatException
                                              or ArgumentOutOfRangeException)
        {
            GameLog.Error($"Ignoring unreadable recovery file '{path}': {exception.Message}");
            return null;
        }
    }

    public void Delete(string levelPath)
    {
        string path = PathFor(levelPath);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>
    ///     A digest of the level that two captures of the same state agree on: a
    ///     level not yet saved gets a fresh identity from every capture, and the
    ///     capture sorts notes by time alone, so ties could come out either way.
    /// </summary>
    public static string Canonical(LevelDocument level)
    {
        LevelDocument copy = LevelSerializer.Deserialize(LevelSerializer.Serialize(level));
        copy.Id = "";
        copy.Slug = "";
        copy.Notes = copy.Notes.OrderBy(n => n.TimeMs).ThenBy(n => n.X).ThenBy(n => n.Y).ThenBy(n => n.Target).ToList();
        return Convert.ToHexString(SHA256.HashData(LevelSerializer.Serialize(copy)));
    }

    private string PathFor(string levelPath)
    {
        string key = string.IsNullOrEmpty(levelPath)
            ? "untitled"
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(levelPath)))[..16];
        return Path.Combine(Directory, key + ".recovery.xmf");
    }

    private enum ChunkId : uint
    {
        Meta = 1,
        Map = 2
    }

    /// <summary>What was autosaved, and when.</summary>
    public sealed class Entry
    {
        public string LevelPath { get; set; } = "";
        public DateTimeOffset SavedAt { get; set; }
        public LevelDocument Level { get; set; } = new();
    }
}
