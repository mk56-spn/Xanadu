using System.IO;

namespace Xanadu.Storage;

/// <summary>Writes a whole binary file, making its directory when it is missing.</summary>
public static class BinaryFile
{
    public static void Save(string path, byte[] data)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, data);
    }
}
