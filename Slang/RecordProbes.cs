using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// The recording half of the probe nodes (<see cref="ProbeNodes"/>): what a stack keeps from frame to frame, which can't
/// be probed at other times (its passes read this frame's lists whatever the clock says). Each frame the clock moves,
/// generated passes copy the stack's kept numbers into a ring of the last <see cref="Frames"/> frames on the GPU
/// (<see cref="List"/>, <c>[lane × frames + frame % frames]</c>): every number of its <c>Stored&lt;T&gt;</c> values
/// (a struct's scalar fields, array items and vector parts), its <c>Counter</c>s, and every count its bundles and blocks
/// tell (<c>pool.count</c>, <c>deaths.heard</c>). <see cref="Head"/> holds (frames recorded, the last frame's time, which
/// code recorded them, lanes). A recorded lane can be replayed, never scrubbed.
/// </summary>
public static partial class RecordProbes
{
    public const string List = "xanaduRecords", Head = "xanaduRecordHead";

    /// <summary>Frames the ring keeps, and the lanes it has room for.</summary>
    public const int Frames = 256, Lanes = 64;

    /// <summary>One number recorded: its label, and the Slang that reads it in a pass.</summary>
    private readonly record struct Watched(string Label, string Read);

    /// <summary>What each recorded lane stands for in <paramref name="source"/>, in order (<c>book.frame</c>, <c>pool.count</c>).</summary>
    public static string[] Labels(string source)
    {
        try
        {
            return StackBuild.Of(source) is { } build ? [.. Declared(source, build).SelectMany(group => group.Numbers).Take(Lanes).Select(watched => watched.Label)] : [];
        }
        catch (BuildProblem)
        {
            return [];
        }
    }

    /// <summary>
    /// The recording passes and their calls, in order: one that decides whether this frame records (the clock moved) and
    /// starts the ring over for new code, one a declaration (each binds only what it reads), one moving the ring on (left
    /// out when there is nothing to record).
    /// </summary>
    public static (string Passes, string Calls) Of(string source, StackBuild? build)
    {
        var groups = build is null ? [] : Declared(source, build);
        var passes = new StringBuilder();
        var calls = new StringBuilder(" xanaduRecord_begin();");
        int lane = 0;
        foreach ((string name, List<Watched> numbers) in groups)
        {
            if (lane >= Lanes)
                break;
            var body = new StringBuilder();
            foreach (Watched watched in numbers.Take(Lanes - lane))
                body.Append($"    {List}[{lane++} * {Frames} + slot] = float({watched.Read});\n");
            passes.Append($"[Pass(1, over: 1)]\nvoid xanaduRecord_{name}(uint t)\n{{\n    if ({Head}.w >> 16 == 0)\n        return;\n"
                          + $"    uint slot = {Head}.x % {Frames};\n{body}}}\n");
            calls.Append($" xanaduRecord_{name}();");
        }

        // Which code recorded the ring: new code (other lanes) starts it over.
        uint stamp = (uint)Stamp(string.Join('\n', groups.SelectMany(group => group.Numbers).Select(watched => watched.Label)));
        string begin = $$"""
            [Pass(1, over: 1)]
            void xanaduRecord_begin(uint t)
            {
                uint4 head = {{Head}};
                if (head.z != {{stamp}}u)
                    head = uint4(0, 0, {{stamp}}u, 0);
                bool moved = head.x == 0 || asfloat(head.y) != time;
                head.y = asuint(time);
                head.w = {{lane}}u | (moved && {{lane}} > 0 ? 0x10000u : 0u);
                {{Head}} = head;
            }

            """;
        // Nothing kept: begin alone, so the lanes show no rows left over from other code.
        if (lane > 0)
        {
            passes.Append($"[Pass(1, over: 1)]\nvoid xanaduRecord_end(uint t)\n{{\n    if ({Head}.w >> 16 != 0)\n        {Head}.x += 1;\n}}\n");
            calls.Append(" xanaduRecord_end();");
        }
        return ($"[Shared] [Size({Lanes * Frames})] List<float> {List};\n[Shared] Stored<uint4> {Head};\n{begin}{passes}", calls.ToString());
    }

    /// <summary>The stack's kept numbers, a group a declaration, in the order declared.</summary>
    private static List<(string Name, List<Watched> Numbers)> Declared(string source, StackBuild build)
    {
        string code = BuildText.Blank(source);
        var found = new List<(int At, string Name, List<Watched> Numbers)>();
        foreach (BuildBuffer buffer in build.Buffers)
        {
            if (buffer.Bundle is not null || buffer.Shared || buffer.Name.StartsWith("xanadu", StringComparison.Ordinal))
                continue;
            List<Watched> numbers = buffer.Stored
                ? [.. Numbers(code, buffer.Type, buffer.Name, buffer.Name, build)]
                : buffer is { Type: "uint", Count: 1 } ? [new Watched(buffer.Name, $"{buffer.Name}[0]")] : [];
            if (numbers.Count > 0)
                found.Add((buffer.Start, buffer.Name, numbers));
        }

        foreach (BuildBundle bundle in build.Bundles)
        {
            string[] told = bundle.Block is { } block ? Told(block) : ["count"];
            if (told.Length > 0)
                found.Add((bundle.Start, bundle.Name, [.. told.Select(property => new Watched($"{bundle.Name}.{property}", $"{bundle.Name}.{property}"))]));
        }

        return [.. found.OrderBy(group => group.At).Select(group => (group.Name, group.Numbers))];
    }

    /// <summary>The numbers in a value of <paramref name="type"/>: itself, a vector's parts, or a struct's fields in turn.</summary>
    private static IEnumerable<Watched> Numbers(string code, string type, string label, string read, StackBuild build, int depth = 0)
    {
        Match scalar = Scalar().Match(type);
        if (scalar.Success)
        {
            int parts = scalar.Groups["n"].Success ? int.Parse(scalar.Groups["n"].Value, CultureInfo.InvariantCulture) : 1;
            for (int k = 0; k < parts; k++)
                yield return parts == 1 ? new Watched(label, read) : new Watched($"{label}.{"xyzw"[k]}", $"{read}.{"xyzw"[k]}");
            yield break;
        }

        Match declared = Regex.Match(code, $@"\bstruct\s+{Regex.Escape(type)}\s*\{{");
        if (!declared.Success || depth > 3)
            yield break;
        int open = declared.Index + declared.Length - 1, close = BuildText.Matching(code, open);
        if (close < 0)
            yield break;
        foreach (Match field in StructField().Matches(code[(open + 1)..close]))
        {
            string fieldType = field.Groups["type"].Value;
            foreach (string name in field.Groups["names"].Value.Split(',', StringSplitOptions.TrimEntries))
            {
                Match array = Regex.Match(name, @"^(?<name>\w+)\s*(?:\[\s*(?<size>\w+)\s*\])?$");
                if (!array.Success)
                    continue;
                string named = array.Groups["name"].Value;
                if (named.StartsWith("pad", StringComparison.Ordinal))
                    continue;
                if (!array.Groups["size"].Success)
                {
                    foreach (Watched watched in Numbers(code, fieldType, $"{label}.{named}", $"{read}.{named}", build, depth + 1))
                        yield return watched;
                    continue;
                }

                string size = array.Groups["size"].Value;
                long items = long.TryParse(size, out long literal) ? literal : build.Constant(size) is { Whole: true } constant ? constant.Long : 0;
                for (long k = 0; k < Math.Min(items, Lanes); k++)
                    foreach (Watched watched in Numbers(code, fieldType, $"{label}.{named}[{k}]", $"{read}.{named}[{k}]", build, depth + 1))
                        yield return watched;
            }
        }
    }

    /// <summary>The counts a block tells: its public uint properties, but its capacity, which never changes.</summary>
    private static string[] Told(BlockType block)
    {
        string code = BuildText.Blank(block.Library.Code);
        Match declared = BuildBlocks.BlockDeclaration().Matches(code).FirstOrDefault(match => match.Groups["name"].Value == block.Name);
        if (declared is null)
            return [];
        int open = declared.Index + declared.Length - 1, close = BuildText.Matching(code, open);
        return close < 0 ? [] : [.. Told().Matches(code[open..close]).Select(match => match.Groups["name"].Value).Where(name => name != "capacity")];
    }

    /// <summary>A stable number for a text (string.GetHashCode changes from run to run).</summary>
    private static int Stamp(string text)
    {
        unchecked
        {
            int hash = (int)2166136261;
            foreach (char c in text)
                hash = (hash ^ c) * 16777619;
            return hash;
        }
    }

    [GeneratedRegex(@"^(?:uint|int|float|bool)(?<n>[234])?$")]
    private static partial Regex Scalar();

    [GeneratedRegex(@"(?<=^|[;{}])\s*(?<type>\w+)\s+(?<names>\w+\s*(?:\[\s*\w+\s*\])?(?:\s*,\s*\w+\s*(?:\[\s*\w+\s*\])?)*)\s*;")]
    private static partial Regex StructField();

    [GeneratedRegex(@"\bpublic\s+property\s+uint\s+(?<name>\w+)\s*\{\s*get\b")]
    private static partial Regex Told();
}
