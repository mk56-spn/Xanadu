using System;
using System.IO;
using Godot;

namespace Xanadu.Particles;

/// <summary>
/// The one owner of the particle library file. The game, the particle editor,
/// the animator and the level editor all ask it for effects by name; none of
/// them resolve the path or touch the file. The file sits in the repository's
/// <c>Assets/Particles</c>, found from whichever Godot project is running, so
/// the tools' own projects read the same file as the game.
/// </summary>
public static class ParticleLibrary
{
    private const string RelativePath = "Assets/Particles/particles.xpf";
    private const string RootMarker = "Xanadu.sln";

    private static XpfLibrary? _shared;

    /// <summary>Raised after <see cref="Save"/> or <see cref="Reload"/> replaces the shared library.</summary>
    public static event Action? Changed;

    /// <summary>The library, loaded on first use; empty when the file is missing or unreadable.</summary>
    public static XpfLibrary Shared => _shared ??= Load();

    /// <summary>The effect called <paramref name="name"/>, or null.</summary>
    public static XpfParticle? Find(string name) => Shared.Find(name);

    /// <summary>Whether the shared library has edits the file does not have yet.</summary>
    public static bool Unsaved { get; private set; }

    /// <summary>Goes up with every change, so whatever draws an effect can tell it must rebuild.</summary>
    public static int Version { get; private set; }

    /// <summary>The shared library was changed in place, as by the particle editor; redraws everything that plays it.</summary>
    public static void MarkEdited()
    {
        Unsaved = true;
        Version++;
        Changed?.Invoke();
    }

    /// <summary>Writes the shared library, edits and all, to the file.</summary>
    public static void Save() => Save(Shared);

    /// <summary>Writes <paramref name="library"/> to the file and makes it the shared one.</summary>
    public static void Save(XpfLibrary library)
    {
        string? path = FilePath();
        if (path is null)
            throw new IOException("The particle library has no home: no repository root above the project.");

        XpfBinary.Save(path, library);
        _shared = library;
        Unsaved = false;
        Version++;
        Changed?.Invoke();
    }

    /// <summary>Reads the file again, for when another tool has saved it.</summary>
    public static void Reload()
    {
        _shared = Load();
        Unsaved = false;
        Version++;
        Changed?.Invoke();
    }

    /// <summary>Replaces the shared library without any file, for tests.</summary>
    public static void UseForTests(XpfLibrary? library)
    {
        _shared = library;
        Unsaved = false;
        Version++;
    }

    private static XpfLibrary Load()
    {
        string? path = FilePath();
        if (path is null || !File.Exists(path))
        {
            GD.PushWarning($"No particle library at {path ?? RelativePath}; particles are off.");
            return new XpfLibrary();
        }

        try
        {
            return XpfBinary.Load(path);
        }
        catch (Exception exception) when (exception is XpfFormatException or IOException or UnauthorizedAccessException)
        {
            GD.PushError($"Particle library {path} could not be read: {exception.Message}");
            return new XpfLibrary();
        }
    }

    private static string? FilePath()
    {
        for (string? directory = ProjectSettings.GlobalizePath("res://");
             !string.IsNullOrEmpty(directory);
             directory = Path.GetDirectoryName(directory.TrimEnd('/', '\\')))
        {
            if (File.Exists(Path.Combine(directory, RootMarker)))
                return Path.Combine(directory, RelativePath);
        }

        return null;
    }
}
