using System;
using System.IO;
using Godot;

namespace Xanadu.Ecs.Storage;

/// <summary>
/// A JSON document stored under a Godot path, written atomically and read
/// best-effort: a missing or unreadable file yields no document rather than an
/// error, so a corrupt settings file can never stop the game from starting.
/// <para>
/// This owns the parts <see cref="JsonFileStore"/> deliberately leaves out:
/// <c>user://</c> resolution and an in-memory-only mode for tests.
/// </para>
/// </summary>
public sealed class PersistedFile<T> where T : class
{
    private readonly string _path;
    private readonly bool _persist;

    /// <param name="path">The target path, or null to use <paramref name="defaultPath"/>.</param>
    /// <param name="defaultPath">Used when <paramref name="path"/> is null.</param>
    /// <param name="persist">When false, <see cref="Save"/> is a no-op.</param>
    public PersistedFile(string? path, string defaultPath, bool persist)
    {
        _path = path ?? defaultPath;
        _persist = persist;
    }

    /// <summary>The stored document, or null when absent or unreadable.</summary>
    public T? Load()
    {
        try
        {
            string path = ResolvePath();
            return File.Exists(path) ? JsonFileStore.Load<T>(path) : null;
        }
        catch (Exception exception)
        {
            Report("read", exception);
            return null;
        }
    }

    public void Save(T value)
    {
        if (!_persist)
            return;

        try
        {
            JsonFileStore.Save(ResolvePath(), value);
        }
        catch (Exception exception)
        {
            // Persistence is best-effort; the session keeps its in-memory value.
            Report("write", exception);
        }
    }

    // Console rather than GD: this also runs in headless unit tests, where
    // reaching into the engine aborts the process.
    private void Report(string verb, Exception exception) =>
        Console.Error.WriteLine($"Could not {verb} {_path}: {exception.Message}");

    private string ResolvePath() =>
        _path.StartsWith("user://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(_path)
            : _path;
}
