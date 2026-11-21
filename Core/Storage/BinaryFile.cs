// Allowed disk access in Core (BannedSymbols.Io.txt): the engine's own byte-file primitive, below Storage, for the host's files (autopsy, arch board); the game's files go through Storage.
#pragma warning disable RS0030
using System.IO;

namespace Xanadu.Core.Storage;

/// <summary>Writes a whole binary file, making its directory when it is missing.
/// The bytes go to a temporary file first and are moved over the real one, so a crash
/// or full disk mid-write leaves the old file whole.</summary>
public static class BinaryFile
{
    public static void Save(string path, byte[] data)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string temporary = path + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, data);
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
            throw;
        }
    }
}
