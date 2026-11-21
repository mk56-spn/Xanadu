using System.IO;
using Xanadu.Core.Storage;
using Xanadu.Model.Animation;
using Xanadu.Serialisation.Animation;

namespace Xanadu.Storage.Animation;

/// <summary>Rig packages (XAF) on disk, at game-folder, <c>user://</c> or absolute paths.</summary>
public static class XafFiles
{
    /// <summary>The package at <paramref name="path"/>, or null when there is no such file. Throws on a bad file.</summary>
    public static XafPackage? Load(string path)
    {
        string full = GamePaths.Full(path);
        return File.Exists(full) ? XafBinary.Read(File.ReadAllBytes(full)) : null;
    }

    public static void Save(string path, XafPackage package) => BinaryFile.Save(GamePaths.Full(path), XafBinary.Write(package));
}
