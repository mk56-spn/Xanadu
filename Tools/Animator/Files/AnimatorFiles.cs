using System;
using Xanadu.Animation;
using Xanadu.Storage;

namespace Xanadu.Tools.Animator.Files;

/// <summary>Reads and writes XAF files at <c>res://</c>, <c>user://</c> or absolute paths.</summary>
public sealed class AnimatorFiles : IAnimatorFiles
{
    public XafPackage? Load(string path)
    {
        string full = GamePaths.Globalize(path);
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
        XafBinary.Save(GamePaths.Globalize(path), package);

    /// <summary>Opens the file with the desktop's default app (an SVG in the art editor).</summary>
    public bool OpenInSystem(string path) =>
        SDL3.SDL.SDL_OpenURL(new Uri(System.IO.Path.GetFullPath(GamePaths.Globalize(path))).AbsoluteUri);

    public string? ReadText(string path)
    {
        string full = GamePaths.Globalize(path);
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
