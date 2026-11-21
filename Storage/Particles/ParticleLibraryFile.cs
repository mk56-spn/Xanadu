using System;
using System.IO;
using Xanadu.Core.Diagnostics;
using Xanadu.Core.Particles;
using Xanadu.Core.Storage;
using Xanadu.Serialisation.Particles;

namespace Xanadu.Storage.Particles;

/// <summary>
/// The particle library's file: <c>Assets/Particles/particles.xpf</c> in the
/// repository, found from whichever game folder is running, so the tools read
/// the same file as the game. Installed into <see cref="ParticleLibrary"/> at start-up.
/// </summary>
public sealed class ParticleLibraryFile : IParticleLibraryFile
{
    private const string RelativePath = "Assets/Particles/particles.xpf";

    public XpfLibrary Load()
    {
        string? path = FilePath();
        if (path is null || !File.Exists(path))
        {
            GameLog.Warning($"No particle library at {path ?? RelativePath}; particles are off.");
            return new XpfLibrary();
        }

        try
        {
            return XpfBinary.Read(File.ReadAllBytes(path));
        }
        catch (Exception exception) when (exception is XpfFormatException or IOException or UnauthorizedAccessException)
        {
            GameLog.Error($"Particle library {path} could not be read: {exception.Message}");
            return new XpfLibrary();
        }
    }

    public void Save(XpfLibrary library)
    {
        string path = FilePath()
            ?? throw new IOException("The particle library has no home: no repository root above the project.");
        try
        {
            BinaryFile.Save(path, XpfBinary.Write(library));
        }
        catch (XpfFormatException exception)
        {
            throw new IOException(exception.Message, exception);
        }
    }

    private static string? FilePath()
    {
        for (string? directory = GamePaths.GameFolder;
             !string.IsNullOrEmpty(directory);
             directory = Path.GetDirectoryName(directory.TrimEnd('/', '\\')))
        {
            if (GamePaths.IsGameFolder(directory))
                return Path.Combine(directory, RelativePath);
        }

        return null;
    }
}
