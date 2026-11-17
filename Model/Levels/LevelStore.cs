using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xanadu.Core.Storage;

namespace Xanadu.Model.Levels;

/// <summary>Reads and writes XMF level maps below user://levels.</summary>
public static class LevelStore
{
    public const string LevelsFolder = "user://levels";
    private const string FallbackFolder = "Game/Levels";

    private static string? _directory;

    public static string Directory => _directory ??= ResolveDirectory();

    private static string ResolveDirectory()
    {
        string userDirectory = GamePaths.Full(LevelsFolder);
        if (IsWritable(userDirectory))
            return userDirectory;

        string fallbackDirectory = GamePaths.Full(FallbackFolder);
        System.IO.Directory.CreateDirectory(fallbackDirectory);
        return fallbackDirectory;
    }

    private static bool IsWritable(string directory)
    {
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, ".write-probe");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string NextUnnamedPath() => NextUnnamedPath(Directory);

    public static string NextUnnamedPath(string directory)
    {
        System.IO.Directory.CreateDirectory(directory);
        for (int index = 1; ; index++)
        {
            string path = Path.Combine(
                directory,
                $"unnamed-{index}{XmfBinary.Extension}");
            if (!File.Exists(path))
                return path;
        }
    }

    public static void EnsureDirectory() =>
        System.IO.Directory.CreateDirectory(Directory);

    /// <summary>Saves a level, replacing whatever file is at <paramref name="path"/>.</summary>
    public static void Save(LevelDocument document, string path) =>
        XmfBinary.Save(path, document);

    public static void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    public static LevelDocument Load(string path) =>
        LevelSerializer.Deserialize(File.ReadAllBytes(path));

    /// <summary>A level with its song opened from the library, ready to play or preview.</summary>
    public static LevelEntry Entry(string path, LevelDocument document)
    {
        LoadedSong? song = SongStore.Shared.Open(document.SongId);
        return new LevelEntry(path, document, song, song is null ? SongStore.Shared.Find(document.SongId) : null);
    }

    /// <summary>Loads every valid level from user data and the bundled Levels folder.</summary>
    public static List<LevelEntry> List()
    {
        var entries = new List<LevelEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        AddFrom(GamePaths.Full(LevelsFolder));
        AddFrom(GamePaths.Full(FallbackFolder));
        AddUndownloaded();

        entries.Sort(static (left, right) =>
        {
            int byAvailability = left.IsBlocked.CompareTo(right.IsBlocked);
            if (byAvailability != 0)
                return byAvailability;

            int byName = string.Compare(
                left.Document.Name,
                right.Document.Name,
                StringComparison.OrdinalIgnoreCase);

            return byName != 0
                ? byName
                : string.Compare(left.Path, right.Path, StringComparison.Ordinal);
        });

        return entries;

        // Songs the catalog knows whose audio isn't here and no level of ours
        // uses: listed greyed out, so the player sees what exists.
        void AddUndownloaded()
        {
            var used = new HashSet<string>(entries.Select(entry => entry.Document.SongId), StringComparer.Ordinal);
            foreach (SongDocument song in SongStore.Shared.LoadAll())
            {
                if (used.Contains(song.Id) || SongStore.Shared.Open(song.Id) is not null)
                    continue;

                entries.Add(new LevelEntry("", new LevelDocument { Name = song.Title, SongId = song.Id }, Known: song));
            }
        }

        void AddFrom(string directory)
        {
            if (!System.IO.Directory.Exists(directory))
                return;

            foreach (string path in System.IO.Directory.EnumerateFiles(
                         directory,
                         "*" + XmfBinary.Extension).OrderBy(
                         static file => file,
                         StringComparer.Ordinal))
            {
                string fullPath = Path.GetFullPath(path);
                if (!seen.Add(fullPath))
                    continue;

                try
                {
                    entries.Add(Entry(fullPath, Load(fullPath)));
                }
                catch (Exception)
                {
                    // Ignore malformed level files and keep the rest of the carousel usable.
                }
            }
        }
    }
}
