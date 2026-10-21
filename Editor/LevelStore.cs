using System;
using System.IO;
using System.Linq;
using Godot;

namespace Writer.Editor;

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
}
