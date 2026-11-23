using System;
using System.IO;
using Xanadu.Core.Storage;
using Xanadu.Model.Animation;
using Xanadu.Serialisation.Items;

namespace Xanadu.Storage.Animation;

/// <summary>Rigs on disk: XIF files at game-folder, <c>user://</c> or absolute paths.</summary>
public static class RigFiles
{
    /// <summary>The rig at <paramref name="path"/>, or null when there is no such file. Throws on a bad file.</summary>
    public static RigPackage? Load(string path)
    {
        string full = GamePaths.Full(path);
        return File.Exists(full) ? Read(File.ReadAllBytes(full)) : null;
    }

    /// <summary>A rig from XIF bytes; throws <see cref="RigFormatException"/> on a bad file or an invalid rig.</summary>
    public static RigPackage Read(byte[] data)
    {
        RigPackage package;
        try
        {
            package = new RigPackage(XifFile.Read(data));
        }
        catch (FormatException exception)
        {
            throw new RigFormatException(exception.Message);
        }
        RigValidation.ThrowIfInvalid(package);
        return package;
    }

    public static byte[] Write(RigPackage package)
    {
        RigValidation.ThrowIfInvalid(package);
        return XifFile.Write(package.Root);
    }

    public static void Save(string path, RigPackage package) => BinaryFile.Save(GamePaths.Full(path), Write(package));

    /// <summary>
    /// Where unsaved work on <paramref name="path"/> is kept between autosaves:
    /// one file per rig under <c>user://recovery</c>, never beside the rig itself.
    /// </summary>
    private static string RecoveryPath(string path)
    {
        var name = new System.Text.StringBuilder();
        foreach (char c in path)
            name.Append(char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_');
        return GamePaths.Full($"user://recovery/{name}");
    }

    /// <summary>Writes unsaved work on <paramref name="path"/> to its recovery file.</summary>
    public static void SaveRecovery(string path, RigPackage package) =>
        BinaryFile.Save(RecoveryPath(path), Write(package));

    /// <summary>The unsaved work kept for <paramref name="path"/> and when it was written, or null when there is none.</summary>
    public static (RigPackage Package, DateTime Written)? LoadRecovery(string path)
    {
        string full = RecoveryPath(path);
        if (!File.Exists(full))
            return null;
        return (Read(File.ReadAllBytes(full)), File.GetLastWriteTime(full));
    }

    /// <summary>Forgets the unsaved work kept for <paramref name="path"/>: it was saved or thrown away.</summary>
    public static void DropRecovery(string path)
    {
        string full = RecoveryPath(path);
        if (File.Exists(full))
            File.Delete(full);
    }
}
