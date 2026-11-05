using System;
using Godot;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Files;

/// <summary>Reads and writes XAF files at <c>res://</c>, <c>user://</c> or absolute paths.</summary>
public sealed class GodotAnimatorFiles : IAnimatorFiles
{
    public XafPackage? Load(string path)
    {
        string full = ProjectSettings.GlobalizePath(path);
        if (!System.IO.File.Exists(full))
            return null;

        try
        {
            return XafBinary.Load(full);
        }
        catch (Exception exception) when (exception is XafFormatException or
                                              System.IO.IOException or
                                              UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(string path, XafPackage package) =>
        XafBinary.Save(ProjectSettings.GlobalizePath(path), package);

    public bool OpenInSystem(string path) =>
        OS.ShellOpen(ProjectSettings.GlobalizePath(path)) == Error.Ok;

    public string? ReadText(string path)
    {
        string full = ProjectSettings.GlobalizePath(path);
        try
        {
            return System.IO.File.Exists(full) ? System.IO.File.ReadAllText(full) : null;
        }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
