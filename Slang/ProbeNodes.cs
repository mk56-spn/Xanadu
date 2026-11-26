using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// Probe nodes: a tool's way to see what a stack's code does over time, whatever way it is written; the clips half is
/// <see cref="ClipProbes"/>. This half shows how a graph's draws move. A body declaring
/// <c>uniform float4 xanaduProbe;</c> (from, span in seconds, beats a second) gets, for each of its draws, a generated
/// <c>[Pass]</c> that runs the draw's own vertex function at <see cref="Samples"/> times through that window, on three
/// corners of each copy, and writes how the copy moved since the window's start (<c>probeMotion</c>: offset, turn, scale)
/// into the frame's <c>[Shared]</c> <see cref="List"/>, a lane of <see cref="Samples"/> per copy, the draws' copies one
/// after another; <see cref="Count"/> says how many lanes there are. Any graph declaring the two reads them (the
/// editor's lanes, <c>Editor/probe_lanes</c>). The game never declares the uniform, so it never probes.
/// </summary>
public static partial class ProbeNodes
{
    /// <summary>The uniform that asks for probes: (from, span, beats a second, unused).</summary>
    public const string Window = "xanaduProbe";

    /// <summary>The shared list the probes write, and the shared count of its lanes.</summary>
    public const string List = "xanaduProbes", Count = "xanaduProbeLanes";

    /// <summary>Samples a lane, and the lanes the list has room for.</summary>
    public const int Samples = 256, Lanes = 64;

    /// <summary>Triangles of a copy tried, in order, for one with an area to measure by.</summary>
    private const int Tries = 8;

    /// <summary>The label the generated passes' lines carry in Slang's errors.</summary>
    private const string Label = "probes (generated)";

    /// <summary>
    /// <paramref name="source"/> lowered for probing: its clips tagged with their sites, the clip pass
    /// (<see cref="ClipProbes"/>), and a motion pass for each draw it can count the copies of, all called at the end of
    /// <c>graph()</c> (a stack without one gets one that runs them). Unchanged without <see cref="Window"/>, or when its
    /// graph can't be read (the compile then says why).
    /// </summary>
    public static string Lower(string source)
    {
        if (!Asked().IsMatch(BuildText.Blank(source)))
            return source;
        string sited = ClipProbes.Sited(source);
        string code = BuildText.Blank(sited);
        StackBuild? build;
        try
        {
            build = StackBuild.Of(sited);
        }
        catch (BuildProblem)
        {
            return source;
        }

        List<(BuildPass Draw, long First, long Copies)> draws = build is null ? [] : [.. Probed(build)];
        var passes = new StringBuilder(ClipProbes.Pass(ShadeSignature.Arguments(source, out _), SlangShaders.Deforms(source), draws));
        var calls = new StringBuilder(" xanaduProbe_clips();");
        long lanes = 0;
        foreach ((BuildPass draw, long first, long copies) in draws)
        {
            string name = $"xanaduProbe_{draw.Name}";
            passes.Append(Pass(name, draw, first, copies));
            calls.Append($" {name}();");
            lanes = first + copies;
        }

        string generated = $"\n#line 1 \"{Label}\"\nimport xanadu.probe;\n[Shared] [Size({Lanes * Samples})] List<float4> {List};\n"
                           + $"[Shared] Stored<uint> {Count};\n"
                           + passes.ToString().Replace("TOTAL", Math.Min(lanes, Lanes).ToString(CultureInfo.InvariantCulture));
        Match entry = Entry().Match(code);
        if (build is null || !entry.Success)
            return sited + generated + $"void graph() {{{calls} }}\n";
        int close = BuildText.Matching(code, code.IndexOf('{', entry.Index + entry.Length));
        if (close < 0)
            return source;
        // On the closing brace's line, so every line after keeps its number.
        return sited[..close] + calls + sited[close..] + generated;
    }

    /// <summary>
    /// What each lane of the probe list stands for in <paramref name="source"/>: its draw and copy (<c>shown 3</c>), in
    /// the lanes' order; empty when it has no graph or no draw to probe. For labels: the GPU only knows lane numbers.
    /// </summary>
    public static string[] Labels(string source)
    {
        try
        {
            if (StackBuild.Of(source) is not { } build)
                return [];
            var labels = new List<string>();
            foreach ((BuildPass draw, long first, long copies) in Probed(build))
                for (long c = 0; c < copies && first + c < Lanes; c++)
                    labels.Add(copies == 1 ? draw.Name : $"{draw.Name} {c}");
            return [.. labels];
        }
        catch (BuildProblem)
        {
            return [];
        }
    }

    /// <summary>The draws probed, in order, with their first lane and copies: those whose copies the CPU can count, while lanes last.</summary>
    private static IEnumerable<(BuildPass Draw, long First, long Copies)> Probed(StackBuild build)
    {
        long lanes = 0;
        foreach (BuildPass draw in build.Passes.Where(pass => pass is { Draw: not null, Library: null, CopiesBuffer: < 0 }))
        {
            if (Copies(build, draw) is not { } copies || lanes >= Lanes)
                continue;
            yield return (draw, lanes, copies);
            lanes += copies;
        }
    }

    /// <summary>A draw's copies when the CPU can work them out from the body's constants; null when it can't (a value, a count on the GPU).</summary>
    private static long? Copies(StackBuild build, BuildPass draw)
    {
        if (draw.Copies is null)
            return 1;
        try
        {
            BuildValue value = draw.Copies.Evaluate(build.Constant);
            return value.Whole && value.Long > 0 ? value.Long : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// The probe pass of one draw: a thread a (copy, sample); each finds a triangle of its copy at the window's start,
    /// then measures where its corners went at its own sample's time.
    /// </summary>
    private static string Pass(string name, BuildPass draw, long first, long copies)
    {
        (int across, int down, _, _) = draw.Draw!.Value;
        string corner = $"probeCorner(3 * t + CORNER, {across}, {down})";
        string Shown(int k) => $"{draw.Name}({corner.Replace("CORNER", k.ToString(CultureInfo.InvariantCulture))}).position";
        return $$"""
            [Pass(64, over: {{copies}} * {{Samples}})]
            void {{name}}(uint i)
            {
                uint c = i / {{Samples}}, s = i % {{Samples}}, lane = {{first}} + c;
                if (i == 0)
                    {{Count}} = TOTAL;
                if (lane >= {{Lanes}})
                    return;
                copy = c;
                probeClock({{Window}}, 0.0);
                float2 q0 = float2(0), q1 = float2(0), q2 = float2(0);
                uint t = 0;
                for (; t < {{Tries}}; t++)
                {
                    q0 = {{Shown(0)}}; q1 = {{Shown(1)}}; q2 = {{Shown(2)}};
                    if (abs(probeArea(q0, q1, q2)) > 1e-3)
                        break;
                }

                float4 moved = float4(0);
                if (t < {{Tries}})
                {
                    probeClock({{Window}}, float(s) / {{Samples - 1}});
                    moved = probeMotion(q0, q1, q2, {{Shown(0)}}, {{Shown(1)}}, {{Shown(2)}});
                }

                {{List}}[lane * {{Samples}} + s] = moved;
            }

            """;
    }

    [GeneratedRegex($@"\buniform\s+float4\s+{Window}\s*;")]
    private static partial Regex Asked();

    [GeneratedRegex(@"\bvoid\s+(?:graph|build)\s*\(\s*\)")]
    private static partial Regex Entry();
}
