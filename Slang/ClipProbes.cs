using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>A <c>Clip name = …;</c> of the code: its name and where the name is (0-based line and column).</summary>
public readonly record struct ClipSite(string Name, int Line, int Column);

/// <summary>
/// The clips half of the probe nodes (<see cref="ProbeNodes"/>). The lowering tags every <c>Clip name = …;</c> with
/// its number in the code (<c>xanaduSited</c>), and a generated pass runs the stack's functions with <c>probing</c> on:
/// thread 0 its shade, thread 1 its deform, the rest one copy of a draw each (its vertex function on one corner). Every
/// clip read meanwhile reports itself (<c>xanadu.anim</c>: <c>probeClip</c>, <c>probeKey</c>), and each thread copies
/// what it saw into its <see cref="Region"/> of the shared <see cref="List"/>, which the editor's lanes report back.
/// The only text read is the sites, for names and the line a row jumps to.
/// </summary>
public static partial class ClipProbes
{
    /// <summary>The shared list the clip probe writes.</summary>
    public const string List = "xanaduClips";

    /// <summary>Threads of the clip pass; float4s each writes: a count, two a clip (16), one a key (16).</summary>
    public const int Threads = 32, Region = 1 + 16 * 2 + 16;

    private const int Clips = 16;

    /// <summary>Every <c>Clip name = …;</c> of the code, in order: what a reported site number stands for.</summary>
    public static List<ClipSite> Sites(string source) => [.. Found(source).Select(found => found.Site)];

    /// <summary><paramref name="source"/> with each site's value wrapped as <c>(…).xanaduSited(n)</c>, every line where it was.</summary>
    public static string Sited(string source)
    {
        var sited = new StringBuilder(source);
        List<(ClipSite Site, int Value, int End)> found = Found(source);
        for (int n = found.Count - 1; n >= 0; n--)
        {
            sited.Insert(found[n].End, $").xanaduSited({n})");
            sited.Insert(found[n].Value, "(");
        }

        return sited.ToString();
    }

    private static List<(ClipSite Site, int Value, int End)> Found(string source)
    {
        string code = BuildText.Blank(source);
        var found = new List<(ClipSite, int, int)>();
        foreach (Match match in Declared().Matches(code))
        {
            int end = StatementEnd(code, match.Index + match.Length);
            if (end < 0)
                continue;
            Group name = match.Groups["name"];
            int lineStart = code.LastIndexOf('\n', name.Index) + 1;
            int line = 0;
            for (int k = 0; k < lineStart; k++)
                line += code[k] == '\n' ? 1 : 0;
            found.Add((new ClipSite(name.Value, line, name.Index - lineStart), match.Index + match.Length, end));
        }

        return found;
    }

    /// <summary>The <c>;</c> ending a statement from <paramref name="from"/>, outside brackets; -1 when there is none.</summary>
    private static int StatementEnd(string code, int from)
    {
        int depth = 0;
        for (int k = from; k < code.Length; k++)
        {
            switch (code[k])
            {
                case '(' or '[' or '{':
                    depth++;
                    break;
                case ')' or ']' or '}':
                    if (--depth < 0)
                        return -1;
                    break;
                case ';' when depth == 0:
                    return k;
            }
        }

        return -1;
    }

    /// <summary>
    /// The clip pass: what each thread runs (the shade's arguments when the code has a shade, whether it deforms, the
    /// probed draws), then the copy of what it saw into its region. Thread 0 also zeroes the lanes' count, which the
    /// motion passes after it set.
    /// </summary>
    public static string Pass(string? shadeArguments, bool deforms, IEnumerable<(BuildPass Draw, long First, long Copies)> draws)
    {
        var calls = new StringBuilder();
        if (shadeArguments is not null)
            calls.Append($$"""
                    if (t == 0)
                    {
                        ItemInput i = tracedInput(float2(PROBE_QUAD * 0.5), float2(0), float2(PROBE_QUAD, 0), float2(0, PROBE_QUAD));
                        shade({{shadeArguments}});
                    }

                """);
        if (deforms)
            calls.Append("        if (t == 1)\n            deform(probeCorner(0, 1, 1));\n");
        foreach ((BuildPass draw, long first, long copies) in draws)
        {
            (int across, int down, _, _) = draw.Draw!.Value;
            calls.Append($"        if (t >= {2 + first} && t < {2 + first + copies})\n        {{\n            copy = t - {2 + first};\n"
                         + $"            {draw.Name}(probeCorner(0, {across}, {down}));\n        }}\n");
        }

        return $$"""
            [Shared] [Size({{Threads * Region}})] List<float4> {{List}};
            [Pass({{Threads}}, over: {{Threads}})]
            void xanaduProbe_clips(uint t)
            {
                if (t == 0)
                    {{ProbeNodes.Count}} = 0;
                probing = true;
                probedClipCount = 0;
                probedKeyCount = 0;
                copy = 0;
                probeClock({{ProbeNodes.Window}}, 0.0);
            {{calls}}
                probing = false;
                uint at = t * {{Region}};
                {{List}}[at] = float4(float(probedClipCount), float(probedKeyCount), 0, 0);
                for (uint k = 0; k < probedClipCount * 2; k++)
                    {{List}}[at + 1 + k] = probedClips[k];
                for (uint k = 0; k < probedKeyCount; k++)
                    {{List}}[at + {{1 + Clips * 2}} + k] = float4(probedKeys[k], 0, 0);
            }

            """;
    }

    /// <summary>
    /// The clips a report holds (the shared list as bytes), one each however many threads saw it: copies of one clip
    /// that start at different times (staggered by copy) are one clip, first start, with the step between their starts.
    /// Named by their site in <paramref name="sites"/>; one with none is named for its order.
    /// </summary>
    public static List<SlangClip> Read(ReadOnlySpan<byte> report, IReadOnlyList<ClipSite> sites)
    {
        ReadOnlySpan<Vector4> all = MemoryMarshal.Cast<byte, Vector4>(report);
        var starts = new Dictionary<(Vector4 Shape, Vector4 Kind), SortedSet<float>>();
        var keys = new Dictionary<int, SortedSet<double>>();
        for (int t = 0; t + Region <= all.Length && t / Region < Threads; t += Region)
        {
            int clips = Math.Min((int)all[t].X, Clips), places = Math.Min((int)all[t].Y, 16);
            for (int c = 0; c < clips; c++)
            {
                Vector4 shape = all[t + 1 + c * 2], kind = all[t + 2 + c * 2];
                (Vector4, Vector4) same = (shape with { X = 0 }, kind);
                if (!starts.TryGetValue(same, out SortedSet<float>? set))
                    starts[same] = set = [];
                set.Add(shape.X);
            }

            for (int k = 0; k < places; k++)
            {
                Vector4 key = all[t + 1 + Clips * 2 + k];
                if (!keys.TryGetValue((int)key.X, out SortedSet<double>? set))
                    keys[(int)key.X] = set = [];
                set.Add(key.Y);
            }
        }

        var found = new List<SlangClip>();
        foreach (((Vector4 shape, Vector4 kind), SortedSet<float> at) in starts.OrderBy(pair => pair.Key.Kind.Z).ThenBy(pair => pair.Value.Min))
        {
            int site = (int)kind.Z, flags = (int)kind.X;
            float[] each = [.. at];
            double step = each.Length < 2 ? 0 : Enumerable.Range(1, each.Length - 1).Min(k => each[k] - each[k - 1]);
            ClipSite named = site >= 0 && site < sites.Count ? sites[site] : new ClipSite($"clip {found.Count + 1}", -1, 0);
            found.Add(new SlangClip(named.Name, named.Line, named.Column, (flags & 1) != 0, shape.Z, each[0], shape.Y, (int)shape.W,
                (flags & 2) != 0, (flags & 4) != 0, step, keys.TryGetValue(site, out SortedSet<double>? places) ? [.. places] : [],
                (int)kind.Y));
        }

        return found;
    }

    [GeneratedRegex(@"(?<![\w.])Clip\s+(?<name>[A-Za-z_]\w*)\s*=(?!=)")]
    private static partial Regex Declared();
}
