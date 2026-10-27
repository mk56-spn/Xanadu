using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Files;

/// <summary>
/// Lists the SVG files under <c>res://Assets</c> and reads their group ids from
/// disk. The animator's own project is the root, so paths are stored as
/// <c>res://</c> and resolve again when the package is loaded elsewhere.
/// </summary>
public sealed class GodotAnimatorSvgCatalog : IAnimatorSvgCatalog
{
    /// <summary>Folder swept for art, relative to the project.</summary>
    public const string AssetsFolder = "res://Assets";

    public IReadOnlyList<string> Files()
    {
        var files = new List<string>();
        string folder = ProjectSettings.GlobalizePath(AssetsFolder);
        if (!Directory.Exists(folder))
            return files;

        try
        {
            foreach (string file in Directory.EnumerateFiles(
                         folder,
                         "*.svg",
                         SearchOption.AllDirectories))
            {
                files.Add(ProjectSettings.LocalizePath(file));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return files;
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    public IReadOnlyList<string> Groups(string svgPath)
    {
        try
        {
            return XafSvgDocument.Groups(
                File.ReadAllText(ProjectSettings.GlobalizePath(svgPath)));
        }
        catch (Exception exception) when (exception is XafFormatException or
                                              IOException or
                                              UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }
}
