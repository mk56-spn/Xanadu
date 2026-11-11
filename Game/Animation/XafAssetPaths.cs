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
    /// Rewrites every <c>res://</c> attachment and chain art path to an absolute path under the
    /// project root that contains <paramref name="xafFullPath"/>. Paths are left
    /// unchanged when no enclosing game folder is found.
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

        foreach (XafChain chain in package.Chains)
        {
            if (chain.SvgPath.StartsWith(ResPrefix, StringComparison.Ordinal))
                chain.SvgPath = Path.Combine(root, chain.SvgPath[ResPrefix.Length..]);
        }
    }

    /// <summary>Nearest directory at or above the file that is a game folder (<see cref="Xanadu.Storage.GamePaths.RootMarker"/>).</summary>
    public static string? ProjectRoot(string fullPath)
    {
        for (string? directory = Path.GetDirectoryName(Path.GetFullPath(fullPath));
             directory is not null;
             directory = Path.GetDirectoryName(directory))
        {
            if (Xanadu.Storage.GamePaths.IsGameFolder(directory))
                return directory;
        }

        return null;
    }
}
