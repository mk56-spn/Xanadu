using System.IO;
using Xanadu.Core.Storage;

namespace Xanadu.Storage.Audio;

/// <summary>Audio on disk, for the audio engine to decode: a game path resolved to a file that is there.</summary>
public static class AudioFiles
{
    /// <summary>The full path of the audio at <paramref name="path"/> (a game path), or null when there is no such file.</summary>
    public static string? Find(string path)
    {
        string full = GamePaths.Full(path);
        return File.Exists(full) ? full : null;
    }
}
