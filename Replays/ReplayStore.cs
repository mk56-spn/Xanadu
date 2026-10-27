using System.Collections.Generic;
using System.Linq;
using System.IO;
using Godot;

namespace Xanadu.Replays;

/// <summary>Reads and writes .xrs files in one directory.</summary>
public sealed class ReplayStore(string directory)
{
    public const string UserFolder = "user://replays";

    private static ReplayStore? _default;

    public static ReplayStore Default =>
        _default ??= new ReplayStore(ProjectSettings.GlobalizePath(UserFolder));

    public string Directory { get; } = directory;

    /// <summary>Writes the replay under a unique name and returns its path.</summary>
    public string Save(Replay replay)
    {
        System.IO.Directory.CreateDirectory(Directory);
        string stem = $"{SafeName(replay.LevelName)}-{replay.RecordedAt.ToLocalTime():yyyyMMdd-HHmmss}";
        string path = Path.Combine(Directory, stem + ReplayFile.Extension);
        for (int suffix = 2; File.Exists(path); suffix++)
            path = Path.Combine(Directory, $"{stem}-{suffix}{ReplayFile.Extension}");

        string temporary = path + ".tmp";
        using (FileStream stream = File.Create(temporary))
            ReplayFile.Write(stream, replay);

        File.Move(temporary, path);
        return path;
    }

    /// <exception cref="InvalidDataException">The file is not a readable replay.</exception>
    public static Replay Load(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return ReplayFile.Read(stream);
    }

    /// <summary>Paths of every replay, newest first.</summary>
    public IReadOnlyList<string> List() =>
        System.IO.Directory.Exists(Directory)
            ? System.IO.Directory
                .GetFiles(Directory, "*" + ReplayFile.Extension)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList()
            : [];

    private static string SafeName(string name)
    {
        string safe = new(name
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')
            .ToArray());
        return safe.Length == 0 ? "replay" : safe;
    }
}
