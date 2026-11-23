using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// Stitches a stack of effect bodies into one, the GPU mirror of a Xif's
/// modules (Docs/xif-modules.md). Each layer is an ordinary effect, the same file
/// that draws on its own: its code goes in a namespace of its own, so helpers
/// never clash, and the stitched <c>deform</c> and <c>shade</c> call the layers'
/// in order, bottom first. A layer's <c>shade</c> finds the colour so far in
/// <c>i.colour</c> (the bottom one the item's own, as when it draws alone) and
/// returns the new one; a layer with no deform moves nothing.
/// </summary>
/// <remarks>
/// Uniforms stay global: they are lifted out of the layers, so params are set by
/// name as for one shader, and two layers declaring the same one (<c>xif_unit</c>)
/// share it, the lower layer's declaration winning. A layer that keeps state
/// (<c>update</c>) can't be stacked. The deform's <c>[Grid]</c> is the finest any
/// layer asks for.
/// </remarks>
public static partial class ShaderStack
{
    /// <summary>A uniform declaration on one line, attributes and all; group 1 its name.</summary>
    [GeneratedRegex(@"^\s*(?:\[[^\n]*\]\s*)*uniform\b[^;\n]*?\b(\w+)\s*(?:\[[^\]]*\])?\s*;\s*(?://.*)?$")]
    private static partial Regex UniformLine();

    /// <summary>A line of attributes only, which belongs to the declaration under it.</summary>
    [GeneratedRegex(@"^\s*(?:\[[^\n]*\]\s*)+$")]
    private static partial Regex AttributeLine();

    [GeneratedRegex(@"\[\s*Grid\s*\(\s*\d+\s*,\s*\d+\s*\)\s*\]")]
    private static partial Regex GridAttribute();

    /// <summary>
    /// One effect body doing what <paramref name="layers"/> (path and source, bottom
    /// first) do stacked; throws when a layer can't be stacked.
    /// </summary>
    public static string Stitch(IReadOnlyList<(string Path, string Source)> layers)
    {
        if (layers.Count == 0)
            throw new ArgumentException("A stack needs at least one layer.", nameof(layers));

        var imports = new List<string>();
        var uniforms = new List<string>();
        var declared = new HashSet<string>(StringComparer.Ordinal);
        var bodies = new StringBuilder();
        var shades = new StringBuilder();
        var deforms = new StringBuilder();
        (int Columns, int Rows)? grid = null;

        for (int index = 0; index < layers.Count; index++)
        {
            (string path, string source) = layers[index];
            string space = $"xanaduLayer{index}";
            if (SlangShaders.KeepsState(source))
                throw new InvalidOperationException($"{path}: keeps state (update), so it can't be stacked.");

            string shadeArguments = ShadeSignature.Arguments(source, out string problem)
                ?? throw new InvalidOperationException($"{path}: {problem}");
            shades.AppendLine($"    i.colour = {space}::shade({shadeArguments});");
            if (SlangShaders.Deforms(source))
            {
                deforms.AppendLine($"    v = {space}::deform(v);");
                if (SlangShaders.GridOf(source) is { } asked)
                    grid = grid is { } had ? (Math.Max(had.Columns, asked.Columns), Math.Max(had.Rows, asked.Rows)) : asked;
            }

            bodies.AppendLine($"namespace {space} {{");
            bodies.AppendLine($"#line 1 \"{path.Replace("\"", "'")}\"");
            foreach (string line in Lift(source, imports, uniforms, declared))
                bodies.AppendLine(line);
            bodies.AppendLine("}");
        }

        var stitched = new StringBuilder();
        foreach (string line in imports)
            stitched.AppendLine(line);
        stitched.AppendLine("#line 1 \"stack\"");
        foreach (string line in uniforms)
            stitched.AppendLine(line);

        // The stitched entries come first: the wrapper reads the first shade's signature.
        stitched.AppendLine("float4 shade(ItemInput i)");
        stitched.AppendLine("{");
        stitched.Append(shades);
        stitched.AppendLine("    return i.colour;");
        stitched.AppendLine("}");
        if (deforms.Length > 0)
        {
            if (grid is { } cells)
                stitched.AppendLine($"[Grid({cells.Columns}, {cells.Rows})]");
            stitched.AppendLine("ItemVertex deform(ItemVertex v)");
            stitched.AppendLine("{");
            stitched.Append(deforms);
            stitched.AppendLine("    return v;");
            stitched.AppendLine("}");
        }

        stitched.Append(bodies);
        return stitched.ToString();
    }

    /// <summary>
    /// A layer's lines with its imports and uniforms (and the attribute lines over
    /// them) taken out, each left as a blank so error lines still match the file.
    /// A uniform already declared lower in the stack is dropped; its <c>[Grid]</c>
    /// goes too, since the stitched deform carries it.
    /// </summary>
    private static List<string> Lift(string source, List<string> imports, List<string> uniforms, HashSet<string> declared)
    {
        string[] lines = source.ReplaceLineEndings("\n").Split('\n');
        var kept = new List<string>(lines.Length);
        var attributes = new List<int>();
        bool inComment = false;
        foreach (string raw in lines)
        {
            string line = raw;
            bool commented = inComment || line.TrimStart().StartsWith("//", StringComparison.Ordinal);
            inComment = BlockCommentOpen(line, inComment);
            if (commented)
            {
                kept.Add(line);
                continue;
            }

            if (line.TrimStart().StartsWith("import ", StringComparison.Ordinal))
            {
                if (!imports.Contains(line.Trim()))
                    imports.Add(line.Trim());
                kept.Add("");
                attributes.Clear();
                continue;
            }

            Match uniform = UniformLine().Match(line);
            if (uniform.Success)
            {
                bool fresh = declared.Add(uniform.Groups[1].Value);
                foreach (int at in attributes)
                {
                    if (fresh)
                        uniforms.Add(kept[at]);
                    kept[at] = "";
                }
                if (fresh)
                    uniforms.Add(line);
                kept.Add("");
                attributes.Clear();
                continue;
            }

            if (AttributeLine().IsMatch(line))
            {
                attributes.Add(kept.Count);
                kept.Add(GridAttribute().Replace(line, ""));
                continue;
            }

            if (line.Trim().Length > 0)
                attributes.Clear();
            kept.Add(GridAttribute().Replace(line, ""));
        }

        return kept;
    }

    /// <summary>Whether a <c>/* … */</c> comment is still open at the end of the line.</summary>
    private static bool BlockCommentOpen(string line, bool open)
    {
        int at = 0;
        while (at < line.Length)
        {
            if (open)
            {
                int end = line.IndexOf("*/", at, StringComparison.Ordinal);
                if (end < 0)
                    return true;
                open = false;
                at = end + 2;
            }
            else
            {
                int lineComment = line.IndexOf("//", at, StringComparison.Ordinal);
                int start = line.IndexOf("/*", at, StringComparison.Ordinal);
                if (start < 0 || (lineComment >= 0 && lineComment < start))
                    return false;
                open = true;
                at = start + 2;
            }
        }
        return open;
    }
}
