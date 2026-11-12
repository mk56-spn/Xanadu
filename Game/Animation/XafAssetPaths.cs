using System;
using System.IO;

namespace Xanadu.Animation;

/// <summary>
/// Resolves a package's SVG paths (relative to the game folder that saved it)
/// against the game folder the package sits in, so a package loads the same
/// art wherever it is opened from.
/// </summary>
public static class XafAssetPaths
{
    /// <summary>
    /// Rewrites every relative attachment and chain art path to an absolute path under the
    /// game folder that contains <paramref name="xafFullPath"/>. Paths are left
    /// unchanged when no enclosing game folder is found.
    /// </summary>
    public static void Rebase(XafPackage package, string xafFullPath)
    {
        string? root = ProjectRoot(xafFullPath);
        if (root is null)
            return;

        foreach (XafAttachment attachment in package.Attachments)
        {
            if (IsRelative(attachment.SvgPath))
                attachment.SvgPath = Path.Combine(root, attachment.SvgPath);
        }

        foreach (XafChain chain in package.Chains)
        {
            if (IsRelative(chain.SvgPath))
                chain.SvgPath = Path.Combine(root, chain.SvgPath);
        }
    }

    private static bool IsRelative(string path) =>
        path.Length > 0 && !Path.IsPathRooted(path) && !path.StartsWith("user://", StringComparison.Ordinal);

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
