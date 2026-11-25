using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// A deform's <c>[Grid(columns, rows)]</c>: the cells each rect it draws is cut into, on the GPU. Since the cut happens
/// there, the CPU never has the item's corners, so a stack with a grid can't run anything that works in the item's box.
/// </summary>
public static class DeformGrid
{
    /// <summary>The most cells a <c>[Grid]</c> may ask for on a side.</summary>
    private const int Most = 256;

    /// <summary>The grid a body puts on its deform, each side clamped to 1..<see cref="Most"/>; null without one.</summary>
    public static (int Columns, int Rows)? Of(string source)
    {
        Match match = Declared(source);
        if (!match.Success)
            return null;
        int columns = Math.Clamp(int.Parse(match.Groups[1].Value), 1, Most);
        int rows = Math.Clamp(int.Parse(match.Groups[2].Value), 1, Most);
        return (columns, rows);
    }

    /// <summary>Throws, worded as Slang's errors are, when a gridded deform sits in a graph that works per item.</summary>
    public static void Check(string name, string source, StackBuild? build)
    {
        Match grid = Declared(source);
        if (!grid.Success || build is null)
            return;
        if (build.Schedule.Any(step => step is BuildDraw or BuildPresent) || build.Passes.Any(pass => pass.Pixels is not null))
            throw new InvalidOperationException(
                $"{SlangShaders.Where(name, source, grid.Index)}: error: a [Grid] deform is cut on the GPU, so this graph's pixel passes, draws and present have no box to work in; drop the [Grid].");
    }

    /// <summary>The <c>[Grid(c, r)]</c> outside comments; comments are blanked, not cut, so its index is the source's.</summary>
    private static Match Declared(string source) =>
        Regex.Match(Blanked(source), @"\[\s*Grid\s*\(\s*(\d+)\s*,\s*(\d+)\s*\)\s*\]");

    private static string Blanked(string source) =>
        Regex.Replace(source, @"/\*.*?\*/|//[^\n]*", comment => new string(' ', comment.Length), RegexOptions.Singleline);
}
