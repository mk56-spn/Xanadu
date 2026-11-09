using System;
using System.Collections.Generic;
using System.IO;
using Xanadu.Animation;
using Xanadu.Storage;

namespace Xanadu.Tools.Animator.Files;

/// <summary>
/// Lists the SVG files under <see cref="AnimatorPaths.Assets"/> and reads their
/// group ids from disk. Paths are stored as <c>res://</c> (the game folder), so
/// they resolve the same in the animator and the game.
/// </summary>
public sealed class AnimatorSvgCatalog : IAnimatorSvgCatalog
{
    public IReadOnlyList<string> Files()
    {
        var files = new List<string>();
        string folder = GamePaths.Globalize(AnimatorPaths.Assets);
        if (!Directory.Exists(folder))
            return files;

        try
        {
            foreach (string file in Directory.EnumerateFiles(
                         folder,
                         "*.svg",
                         SearchOption.AllDirectories))
            {
                files.Add(Localize(file));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return files;
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    public long Stamp(string svgPath)
    {
        string path = GamePaths.Globalize(svgPath);
        return File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0L;
    }

    public IReadOnlyList<string> Groups(string svgPath)
    {
        try
        {
            return XafSvgDocument.Groups(
                File.ReadAllText(GamePaths.Globalize(svgPath)));
        }
        catch (Exception exception) when (exception is XafFormatException or
                                              IOException or
                                              UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>A file under the game folder as a <c>res://</c> path.</summary>
    private static string Localize(string file) =>
        "res://" + Path.GetRelativePath(GamePaths.Globalize("res://"), file).Replace('\\', '/');
}
