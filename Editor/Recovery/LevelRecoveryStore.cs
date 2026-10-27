using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Godot;
using Xanadu.Ecs.Storage;
using Xanadu.Levels;

namespace Xanadu.Editor.Recovery;

/// <summary>
/// Autosaved copies of levels with unsaved edits, one file per level, kept
/// away from the level files themselves so a recovery can never overwrite
/// real work. An untitled level has a single shared slot.
/// </summary>
public sealed class LevelRecoveryStore(string directory)
{
    public const string UserFolder = "user://recovery";

    private static LevelRecoveryStore? _default;

    public static LevelRecoveryStore Default =>
        _default ??= new LevelRecoveryStore(ProjectSettings.GlobalizePath(UserFolder));

    public string Directory { get; } = directory;

    /// <summary>What was autosaved, and when.</summary>
    public sealed class Entry
    {
        public string LevelPath { get; set; } = "";
        public DateTimeOffset SavedAt { get; set; }
        public LevelDocument Level { get; set; } = new();
    }

    public void Write(string levelPath, LevelDocument level, DateTimeOffset now) =>
        JsonFileStore.Save(PathFor(levelPath), new Entry
        {
            LevelPath = levelPath,
            SavedAt = now,
            Level = level,
        });

    /// <summary>The autosave for a level, or null if there is none or it cannot be read.</summary>
    public Entry? Read(string levelPath)
    {
        string path = PathFor(levelPath);
        if (!File.Exists(path))
            return null;

        try
        {
            Entry? entry = JsonFileStore.Load<Entry>(path);
            if (entry is null)
                return null;

            entry.Level = LevelSerializer.Normalize(entry.Level ?? new LevelDocument());
            return entry;
        }
        catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException)
        {
            GD.PushError($"Ignoring unreadable recovery file '{path}': {exception.Message}");
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
    /// A form of the level that two captures of the same state agree on: a
    /// level not yet saved gets a fresh identity from every capture, and the
    /// capture sorts notes by time alone, so ties could come out either way.
    /// </summary>
    public static string Canonical(LevelDocument level)
    {
        LevelDocument copy = LevelSerializer.Deserialize(LevelSerializer.Serialize(level));
        copy.Id = "";
        copy.Slug = "";
        copy.Notes = copy.Notes
            .OrderBy(n => n.TimeMs).ThenBy(n => n.X).ThenBy(n => n.Y)
            .ThenBy(n => n.Kind).ThenBy(n => n.Target).ThenBy(n => n.DashDirection)
            .ToList();
        copy.Mines = copy.Mines
            .OrderBy(m => m.TimeMs).ThenBy(m => m.X).ThenBy(m => m.Y)
            .ToList();
        return LevelSerializer.Serialize(copy);
    }

    private string PathFor(string levelPath)
    {
        string key = string.IsNullOrEmpty(levelPath)
            ? "untitled"
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(levelPath)))[..16];
        return Path.Combine(Directory, key + ".recovery.json");
    }
}
