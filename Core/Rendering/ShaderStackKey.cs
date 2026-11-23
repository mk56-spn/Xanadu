using System.Collections.Generic;

namespace Xanadu.Core.Rendering;

/// <summary>
/// The name a stack of shaders goes by wherever one shader path would: a
/// material, a draw run, the pipelines' compile cache. <c>stack:a.slang|b.slang</c>
/// is <c>a</c> with <c>b</c> over it; the host stitches the files into one
/// shader the first time the name is drawn (Slang's <c>ShaderStack</c>).
/// </summary>
public static class ShaderStackKey
{
    public const string Prefix = "stack:";

    /// <summary>
    /// The name for <paramref name="paths"/>, bottom first: the one path itself when
    /// there is only one, "" when there is none.
    /// </summary>
    public static string Of(IReadOnlyList<string> paths) => paths.Count switch
    {
        0 => "",
        1 => paths[0],
        _ => Prefix + string.Join('|', paths),
    };

    /// <summary>The paths a stack's name stands for, bottom first; false for a plain shader path.</summary>
    public static bool TryParse(string key, out string[] paths)
    {
        if (!key.StartsWith(Prefix, StringComparison.Ordinal))
        {
            paths = [];
            return false;
        }

        paths = key[Prefix.Length..].Split('|', StringSplitOptions.RemoveEmptyEntries);
        return true;
    }
}
