using System.IO;
using Xanadu.Core.Diagnostics;

namespace Xanadu.Core.Particles;

/// <summary>Where the particle library is kept: the one file behind <see cref="ParticleLibrary"/>, which Storage provides.</summary>
public interface IParticleLibraryFile
{
    /// <summary>The library as the file holds it; empty when there is no file or it can't be read.</summary>
    XpfLibrary Load();

    /// <summary>Writes the library to the file. Throws <see cref="IOException"/> when it can't.</summary>
    void Save(XpfLibrary library);
}

/// <summary>
/// The one particle library everyone shares. The game, the particle editor,
/// the animator and the level editor all ask it for effects by name; none of
/// them touch the file, which is the <see cref="IParticleLibraryFile"/> an app
/// installs at start-up (<see cref="Use"/>). Without one, the library starts
/// empty and can't be saved.
/// </summary>
public static class ParticleLibrary
{
    private static XpfLibrary? _shared;
    private static IParticleLibraryFile? _file;

    /// <summary>Raised after <see cref="Save()"/> or <see cref="Reload"/> replaces the shared library.</summary>
    public static event Action? Changed;

    /// <summary>The library, loaded on first use; empty when there is no file or it is unreadable.</summary>
    public static XpfLibrary Shared => _shared ??= Load();

    /// <summary>The effect called <paramref name="name"/>, or null.</summary>
    public static XpfParticle? Find(string name) => Shared.Find(name);

    /// <summary>Whether the shared library has edits the file does not have yet.</summary>
    public static bool Unsaved { get; private set; }

    /// <summary>Goes up with every change, so whatever draws an effect can tell it must rebuild.</summary>
    public static int Version { get; private set; }

    /// <summary>Installs the file the library loads from and saves to; the next read loads it.</summary>
    public static void Use(IParticleLibraryFile file)
    {
        _file = file;
        _shared = null;
        Unsaved = false;
        Version++;
    }

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
        if (_file is null)
            throw new IOException("The particle library has no file: the app didn't install one.");

        _file.Save(library);
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
        if (_file is not null)
            return _file.Load();

        GameLog.Warning("No particle library file is installed; particles are off.");
        return new XpfLibrary();
    }
}
