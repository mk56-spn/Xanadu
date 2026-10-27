using System;
using System.IO;

namespace Xanadu.Animation;

/// <summary>
/// Resolves a package's <c>res://</c> SVG paths against the Godot project that
/// saved it, so packages authored in the animator tool's project load from
/// any other project.
/// </summary>
public static class XafAssetPaths
{
    private const string ResPrefix = "res://";

    /// <summary>
    /// Rewrites every <c>res://</c> attachment path to an absolute path under the
    /// project root that contains <paramref name="xafFullPath"/>. Paths are left
    /// unchanged when no enclosing <c>project.godot</c> is found.
    /// </summary>
    public static void Rebase(XafPackage package, string xafFullPath)
    {
        string? root = ProjectRoot(xafFullPath);
        if (root is null)
            return;

        foreach (XafAttachment attachment in package.Attachments)
        {
            if (!attachment.SvgPath.StartsWith(ResPrefix, StringComparison.Ordinal))
                continue;

            attachment.SvgPath = Path.Combine(root, attachment.SvgPath[ResPrefix.Length..]);
        }
    }

    /// <summary>Nearest directory at or above the file holding a <c>project.godot</c>.</summary>
    public static string? ProjectRoot(string fullPath)
    {
        for (string? directory = Path.GetDirectoryName(Path.GetFullPath(fullPath));
             directory is not null;
             directory = Path.GetDirectoryName(directory))
        {
            if (File.Exists(Path.Combine(directory, "project.godot")))
                return directory;
        }

        return null;
    }
}
