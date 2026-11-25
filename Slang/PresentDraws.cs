using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// <c>present(colour)</c> and <c>present(colour, depth)</c> are sugar for a library draw (Docs/render-graph.md, stage C):
/// before a stack compiles, each distinct call becomes a generated <c>[CoversBox] [Draw(1, 1, …)]</c> drawn onto
/// <c>frame</c>, its shade reading the images over the item's box: a box-sized one texel for pixel, a scaled or fixed
/// one stretched over the box (the colour <c>filtered</c>, the depth nearest), the depth written through an
/// <c>SV_Depth</c> field. Pixels whose colour is clear write nothing.
/// </summary>
public static class PresentDraws
{
    /// <summary>The label the generated draws' lines carry in Slang's errors.</summary>
    public const string Label = "present (generated)";

    /// <summary>
    /// <paramref name="source"/> with every <c>present(…);</c> in its graph called as a generated draw, the draws after
    /// its last line; unchanged when it presents nothing, or when its graph can't be read (the compile then says why).
    /// </summary>
    public static string Lower(string source)
    {
        StackBuild? build;
        try
        {
            build = StackBuild.Of(source);
        }
        catch (BuildProblem)
        {
            return source;
        }

        if (build is null)
            return source;
        var presents = new List<BuildPresent>();
        Collect(build.Schedule, presents);
        if (presents.Count == 0)
            return source;

        // Back to front, so each call's place stays where the parse found it.
        presents.Sort((a, b) => b.Index.CompareTo(a.Index));
        var lowered = new StringBuilder(source);
        var drawn = new Dictionary<(int, int), string>();
        foreach (BuildPresent present in presents)
        {
            Match call = Regex.Match(source[present.Index..], @"^present\s*\([^;]*\)\s*;");
            if (!call.Success)
                return source;
            if (!drawn.TryGetValue((present.Colour, present.Depth), out string? name))
                drawn[(present.Colour, present.Depth)] = name = NameOf(build, present);
            // The lines the call spanned stay, so everything after it keeps its line.
            string newlines = new('\n', call.Value.Split('\n').Length - 1);
            lowered.Remove(present.Index, call.Length).Insert(present.Index, $"{name}(frame);{newlines}");
        }

        lowered.Append($"\n#line 1 \"{Label}\"\n");
        foreach (((int colour, int depth), string name) in drawn)
            lowered.Append(Draw(build, name, build.Images[colour], depth < 0 ? null : build.Images[depth]));
        return lowered.ToString();
    }

    private static void Collect(IEnumerable<BuildStep> steps, List<BuildPresent> into)
    {
        foreach (BuildStep step in steps)
        {
            switch (step)
            {
                case BuildPresent present:
                    into.Add(present);
                    break;
                case BuildBranch branch:
                    Collect(branch.Yes, into);
                    Collect(branch.No, into);
                    break;
                case BuildLoop loop:
                    Collect(loop.Body, into);
                    break;
            }
        }
    }

    /// <summary>A draw per pair of images, named for them: <c>present_shown</c>, <c>present_shown_depth</c>.</summary>
    private static string NameOf(StackBuild build, BuildPresent present) =>
        present.Depth < 0 ? $"present_{build.Images[present.Colour].Name}" : $"present_{build.Images[present.Colour].Name}_{build.Images[present.Depth].Name}";

    /// <summary>The generated draw and its shade.</summary>
    private static string Draw(StackBuild build, string name, BuildImage colour, BuildImage? depth)
    {
        string read = colour.Size.Box
            ? $"{colour.Name}[group.pixel]"
            : $"filtered({colour.Name}, (float2(group.pixel) + 0.5) * {Extent(colour)} / canvas.size, {Extent(colour)})";
        if (depth is null)
            return $$"""
                [CoversBox] [Draw(1, 1, shade: {{name}}_look)]
                ItemVertex {{name}}(ItemVertex v) { return v; }
                float4 {{name}}_look(ItemInput i, Canvas canvas) { return {{read}}; }

                """;

        string far = depth.Size.Box
            ? $"{depth.Name}[group.pixel]"
            : $"{depth.Name}[uint2((float2(group.pixel) + 0.5) * {Extent(depth)} / canvas.size)]";
        return $$"""
            struct {{name}}_texel { float4 shown; float far : SV_Depth; };
            [CoversBox] [Draw(1, 1, shade: {{name}}_look)]
            ItemVertex {{name}}(ItemVertex v) { return v; }
            {{name}}_texel {{name}}_look(ItemInput i, Canvas canvas)
            {
                {{name}}_texel o;
                o.shown = {{read}};
                if (o.shown.a <= 0.0)
                    discard;
                o.far = {{far}};
                return o;
            }

            """;
    }

    /// <summary>The texels an image covers, in the draw's shade: fixed, or the box (its <c>canvas.size</c>) scaled and rounded up.</summary>
    private static string Extent(BuildImage image) => image.Size.Fixed
        ? $"float2({image.Size.Width}, {image.Size.Height})"
        : $"max(ceil(canvas.size * {image.Size.Scale.ToString("R", CultureInfo.InvariantCulture)}), 1.0)";
}
