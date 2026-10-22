using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace Writer.Levels;

/// <summary>A loaded level file and its document.</summary>
/// <summary>Reads and writes levels below user://levels.</summary>
public static class LevelStore
{
    public const string LevelsFolder = "user://levels";
    private const string FallbackFolder = "res://Levels";

    private static string? _directory;

    public static string Directory => _directory ??= ResolveDirectory();

    private static string ResolveDirectory()
    {
        string userDirectory = ProjectSettings.GlobalizePath(LevelsFolder);
        if (IsWritable(userDirectory))
            return userDirectory;

        string fallbackDirectory = ProjectSettings.GlobalizePath(FallbackFolder);
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

    public static string DefaultPath(string name)
    {
        string safe = new(name
            .Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_')
            .ToArray());

        safe = safe.Trim();
        if (string.IsNullOrEmpty(safe))
            safe = "untitled";

        return Path.Combine(Directory, safe + ".json");
    }

    public static void EnsureDirectory() =>
        System.IO.Directory.CreateDirectory(Directory);

    public static void Save(LevelDocument document, string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            System.IO.Directory.CreateDirectory(directory);

        string temporaryPath = path + ".tmp";

        try
        {
            File.WriteAllText(temporaryPath, LevelSerializer.Serialize(document));
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            throw;
        }
    }

    public static LevelDocument Load(string path) =>
        LevelSerializer.Deserialize(File.ReadAllText(path));

    /// <summary>Loads every valid level from user data and the bundled Levels folder.</summary>
    public static List<LevelEntry> List()
    {
        var songs = new SongStore();
        var entries = new List<LevelEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        AddFrom(ProjectSettings.GlobalizePath(LevelsFolder));
        AddFrom(ProjectSettings.GlobalizePath(FallbackFolder));

        entries.Sort(static (left, right) =>
        {
            int byName = string.Compare(
                left.Document.Name,
                right.Document.Name,
                StringComparison.OrdinalIgnoreCase);

            return byName != 0
                ? byName
                : string.Compare(left.Path, right.Path, StringComparison.Ordinal);
        });

        return entries;

        void AddFrom(string directory)
        {
            if (!System.IO.Directory.Exists(directory))
                return;

            foreach (string path in System.IO.Directory.EnumerateFiles(
                         directory,
                         "*.json").OrderBy(
                         static file => file,
                         StringComparer.Ordinal))
            {
                string fullPath = Path.GetFullPath(path);
                if (!seen.Add(fullPath))
                    continue;

                try
                {
                    LevelDocument document = Load(fullPath);
                    entries.Add(new LevelEntry(
                        fullPath,
                        document,
                        songs.Find(document.SongId)));
                }
                catch (Exception)
                {
                    // Ignore malformed level files and keep the rest of the carousel usable.
                }
            }
        }
    }
}
