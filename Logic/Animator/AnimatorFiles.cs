using Xanadu.Model.Animation;
using Xanadu.Storage.Animation;

namespace Xanadu.Logic.Animator;

/// <summary>
/// Reads and writes the animator's rigs (XIF files) at game-folder, <c>user://</c> or absolute paths,
/// through Storage's <see cref="RigFiles"/>; a file that can't be read loads as null.
/// </summary>
public static class AnimatorFiles
{
    public static RigPackage? Load(string path)
    {
        try
        {
            return RigFiles.Load(path);
        }
        catch (Exception exception) when (exception is RigFormatException or
                                              System.IO.IOException or
                                              UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Save(string path, RigPackage package) => RigFiles.Save(path, package);

    /// <summary>Keeps unsaved work on <paramref name="path"/> where a crash cannot lose it.</summary>
    // Recovery is best effort: a failed autosave or a bad recovery file must
    // never stop the animator, so these swallow the same errors Load does.
    public static void SaveRecovery(string path, RigPackage package)
    {
        try
        {
            RigFiles.SaveRecovery(path, package);
        }
        catch (Exception exception) when (exception is RigFormatException or
                                              System.IO.IOException or
                                              UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The unsaved work kept for <paramref name="path"/>, or null.</summary>
    public static (RigPackage Package, DateTime Written)? LoadRecovery(string path)
    {
        try
        {
            return RigFiles.LoadRecovery(path);
        }
        catch (Exception exception) when (exception is RigFormatException or
                                              System.IO.IOException or
                                              UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Forgets the unsaved work kept for <paramref name="path"/>.</summary>
    public static void DropRecovery(string path)
    {
        try
        {
            RigFiles.DropRecovery(path);
        }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException)
        {
        }
    }
}
