using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// The colour half of the probe nodes (<see cref="ProbeNodes"/>): what a shade paints over time, which catches motion
/// that never reaches a corner (a stripe sliding inside a shade). A generated pass per shade (the stack's own
/// <c>shade</c>, then each draw's whose shade returns one colour, a lane per copy) runs it at <see cref="Points"/>
/// places on the probe's quad (<c>probeInput</c>) at every sample time of the window, into the shared
/// <see cref="List"/>: <c>[(lane × samples + sample) × points + point]</c>; <see cref="Count"/> says how many lanes.
/// </summary>
public static class ColourProbes
{
    public const string List = "xanaduProbeColours", Count = "xanaduColourLanes";

    /// <summary>Places a lane is looked at.</summary>
    public const int Points = 4;

    /// <summary>A shade that can be probed: its function, what it is passed, and its lanes.</summary>
    private readonly record struct Shaded(string Label, string Function, string Arguments, long First, long Copies, bool Draw);

    /// <summary>What each colour lane stands for in <paramref name="source"/>, in order (<c>shade</c>, <c>look 3</c>).</summary>
    public static string[] Labels(string source)
    {
        try
        {
            return [.. Lanes(source, StackBuild.Of(source)).SelectMany(shaded => shaded.Copies == 1 && !shaded.Draw
                ? [shaded.Label]
                : Enumerable.Range(0, (int)shaded.Copies).Select(c => $"{shaded.Label} {c}"))];
        }
        catch (BuildProblem)
        {
            return [];
        }
    }

    /// <summary>The colour passes and the call of each, in order.</summary>
    public static (string Passes, string Calls) Of(string source, StackBuild? build)
    {
        var passes = new StringBuilder();
        var calls = new StringBuilder();
        List<Shaded> lanes = Lanes(source, build);
        long total = lanes.Count == 0 ? 0 : Math.Min(lanes[^1].First + lanes[^1].Copies, ProbeNodes.Lanes);
        if (lanes.Count == 0)
            passes.Append($"[Pass(1, over: 1)]\nvoid xanaduColour_none(uint t) {{ {Count} = 0; }}\n");
        foreach (Shaded shaded in lanes)
        {
            string name = $"xanaduColour_{shaded.Function}";
            int samples = ProbeNodes.Samples;
            passes.Append($$"""
                [Pass(64, over: {{shaded.Copies}} * {{samples * Points}})]
                void {{name}}(uint t)
                {
                    uint c = t / {{samples * Points}}, s = (t / {{Points}}) % {{samples}}, p = t % {{Points}}, lane = {{shaded.First}} + c;
                    if (t == 0)
                        {{Count}} = {{total}};
                    if (lane >= {{ProbeNodes.Lanes}})
                        return;
                    copy = c;
                    probeClock({{ProbeNodes.Window}}, float(s) / {{samples - 1}});
                    ItemInput i = probeInput(p);
                    {{List}}[(lane * {{samples}} + s) * {{Points}} + p] = float4({{shaded.Function}}({{shaded.Arguments}}));
                }

                """);
        }

        calls.Append(lanes.Count == 0 ? " xanaduColour_none();" : string.Concat(lanes.Select(shaded => $" xanaduColour_{shaded.Function}();")));
        return ($"[Shared] [Size({ProbeNodes.Lanes * ProbeNodes.Samples * Points})] List<float4> {List};\n[Shared] Stored<uint> {Count};\n{passes}",
            calls.ToString());
    }

    /// <summary>The stack's shade, then each probed draw whose shade returns one colour, each with its first lane.</summary>
    private static List<Shaded> Lanes(string source, StackBuild? build)
    {
        var lanes = new List<Shaded>();
        long next = 0;
        if (ShadeSignature.Arguments(source, out _) is { } own)
            lanes.Add(new Shaded("shade", "shade", own, next++, 1, false));
        if (build is null)
            return lanes;
        string code = BuildText.Blank(source);
        foreach ((BuildPass draw, _, long copies) in ProbeNodes.Probed(build))
        {
            (_, _, string shade, DrawOutput[] outputs) = draw.Draw!.Value;
            if (outputs is not [{ Field: null }] || lanes.Exists(had => had.Function == shade) || next >= ProbeNodes.Lanes)
                continue;
            Match at = Regex.Match(code, $@"\b\w+\s+{Regex.Escape(shade)}\s*\(([^)]*)\)");
            if (!at.Success || ShadeSignature.Arguments("float4 shade(" + at.Groups[1].Value + ")", out _) is not { } arguments)
                continue;
            lanes.Add(new Shaded(shade, shade, arguments, next, copies, true));
            next += copies;
        }

        return lanes;
    }
}
