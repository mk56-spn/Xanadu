using System;
using Xanadu.Core.Storage;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Storage.Animation;

namespace Xanadu.Logic.Animator;

/// <summary>Reads and writes rigs (XIF files) at game-folder, <c>user://</c> or absolute paths.</summary>
public sealed class AnimatorFiles : IAnimatorFiles
{
    public RigPackage? Load(string path)
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

    public void Save(string path, RigPackage package) => RigFiles.Save(path, package);

    // Recovery is best effort: a failed autosave or a bad recovery file must
    // never stop the animator, so these swallow the same errors Load does.
    public void SaveRecovery(string path, RigPackage package)
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

    public (RigPackage Package, DateTime Written)? LoadRecovery(string path)
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

    public void DropRecovery(string path)
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
