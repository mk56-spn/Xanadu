using System;
using Godot;
using Writer.Animation;

namespace Writer.Tools.Animator.Files;

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
}
