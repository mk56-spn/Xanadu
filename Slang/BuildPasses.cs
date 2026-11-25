using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>A pass's parameter after its thread: a uint its calls pass, or (in a library) a buffer the call lends it.</summary>
/// <param name="BufferType">The element type of a <c>RWStructuredBuffer&lt;T&gt;</c> parameter; null for a uint.</param>
public sealed record BuildParameter(string Name, string? BufferType)
{
    public bool IsBuffer => BufferType is not null;
}

/// <summary>The <c>static const</c>s of a brick or library module, worked out on demand.</summary>
internal sealed partial class BuildConstants(string code)
{
    private readonly Dictionary<string, BuildValue?> _known = [];
    private readonly HashSet<string> _working = [];

    [GeneratedRegex(@"\bstatic\s+const\s+(?:uint|int|float|bool|half)\s+(?<name>\w+)\s*=\s*(?<value>[^;]+);")]
    private static partial Regex ConstantDeclaration();

    /// <summary>The constant by that name, worked out; null when there is none or it can't be.</summary>
    public BuildValue? Of(string name)
    {
        if (_known.TryGetValue(name, out BuildValue? known))
            return known;
        if (!_working.Add(name))
            return null;
        BuildValue? value = null;
        foreach (Match match in ConstantDeclaration().Matches(code))
        {
            if (match.Groups["name"].Value != name)
                continue;
            try
            {
                value = BuildExpression.Parse(match.Groups["value"].Value).Evaluate(Of);
            }
            catch (System.FormatException)
            {
            }

            break;
        }

        _working.Remove(name);
        _known[name] = value;
        return value;
    }
}

/// <summary>Reads the <c>[Pass]</c> declarations of a BUILD brick or a library module.</summary>
internal static partial class BuildPasses
{
    [GeneratedRegex(@"(?<attribute>\[\s*Pass\s*\((?<args>[^\]]*)\)\s*\])\s*(?:public\s+)?void\s+(?<name>\w+)\s*\((?<params>[^)]*)\)")]
    private static partial Regex PassDeclaration();

    [GeneratedRegex(@"\[\s*Pass\s*\(")]
    public static partial Regex PassMarker();

    [GeneratedRegex(@"(?<attribute>\[\s*Pixels\s*\(\s*(?<w>\d+)\s*,\s*(?<h>\d+)\s*\)\s*\])\s*void\s+(?<name>\w+)\s*\((?<params>[^)]*)\)")]
    private static partial Regex PixelsDeclaration();

    /// <summary>The most threads a pixel pass's tile holds.</summary>
    private const int MostTileThreads = 1024;

    [GeneratedRegex(@"^RWStructuredBuffer\s*<\s*(?<type>[\w:.]+)\s*>\s+(?<name>\w+)$")]
    private static partial Regex BufferParameter();

    public static BuildExpression ParseAt(string text, int index)
    {
        try
        {
            return BuildExpression.Parse(text);
        }
        catch (System.FormatException problem)
        {
            throw new BuildProblem(index, problem.Message + ".");
        }
    }

    public static BuildValue Evaluate(BuildExpression expression, int index, System.Func<string, BuildValue?> lookup)
    {
        try
        {
            return expression.Evaluate(lookup);
        }
        catch (System.FormatException problem)
        {
            throw new BuildProblem(index, problem.Message + ".");
        }
    }

    /// <summary>
    /// Every pass in <paramref name="code"/> (comments blanked). A brick's passes take uints after
    /// their thread; a library's (<paramref name="library"/>) may take buffers too, the calls lending
    /// it the brick's, and its <c>over:</c> and <c>groups:</c> may name its own parameters.
    /// </summary>
    public static List<BuildPass> Read(string code, System.Func<string, BuildValue?> constant, bool library)
    {
        var passes = new List<BuildPass>();
        var declared = PassDeclaration().Matches(code);
        foreach (Match marker in PassMarker().Matches(code))
            if (!declared.Any(match => match.Index == marker.Index))
                throw new BuildProblem(marker.Index, "write a pass as [Pass(threads, over: n)] void name(uint i) (or groups: g in place of over: n).");

        foreach (Match match in declared)
        {
            string name = match.Groups["name"].Value;
            if (name is "build" or "graph" or "present")
                throw new BuildProblem(match.Index, $"{name} is taken by the schedule; give the pass another name.");
            if (passes.Any(pass => pass.Name == name))
                throw new BuildProblem(match.Index, $"the pass {name} is declared twice.");
            (int threads, BuildExpression? over, BuildExpression? groups) = Shape(name, match.Groups["args"], constant);
            BuildParameter[] parameters = Parameters(name, match.Groups["params"], library);
            passes.Add(new BuildPass(name, threads, over, groups, parameters, match.Index, match.Index + match.Groups["attribute"].Length));
        }

        if (!library)
            ReadPixels(code, passes);
        return passes;
    }

    /// <summary>
    /// The brick's pixel passes: <c>[Pixels(w, h)] void name(ItemInput i, Canvas canvas)</c>, compute over the item's
    /// pixels in tiles of w × h, taking what <c>shade</c> may take.
    /// </summary>
    private static void ReadPixels(string code, List<BuildPass> passes)
    {
        var declared = PixelsDeclaration().Matches(code);
        foreach (Match marker in Regex.Matches(code, @"\[\s*Pixels\s*\("))
            if (!declared.Any(match => match.Index == marker.Index))
                throw new BuildProblem(marker.Index, "write a pixel pass as [Pixels(8, 8)] void name(ItemInput i, Canvas canvas).");

        foreach (Match match in declared)
        {
            string name = match.Groups["name"].Value;
            if (name is "build" or "graph" or "present")
                throw new BuildProblem(match.Index, $"{name} is taken; give the pass another name.");
            if (passes.Any(pass => pass.Name == name))
                throw new BuildProblem(match.Index, $"the pass {name} is declared twice.");
            int width = int.Parse(match.Groups["w"].Value), height = int.Parse(match.Groups["h"].Value);
            if (width < 1 || height < 1 || width * height > MostTileThreads)
                throw new BuildProblem(match.Index, $"a tile holds 1 to {MostTileThreads} threads, not {width} × {height}.");
            passes.Add(new BuildPass(name, width * height, null, null, [], match.Index, match.Index + match.Groups["attribute"].Length)
            {
                Pixels = (width, height),
                Signature = match.Groups["params"].Value,
            });
        }
    }

    private static (int, BuildExpression?, BuildExpression?) Shape(string name, Group args, System.Func<string, BuildValue?> constant)
    {
        string[] parts = BuildText.SplitTop(args.Value);
        BuildExpression? over = null, groups = null;
        int threads = 0;
        int at = args.Index;
        for (int k = 0; k < parts.Length; at += parts[k].Length + 1, k++)
        {
            string part = parts[k];
            Match named = Regex.Match(part, @"^\s*(\w+)\s*:(.*)$", RegexOptions.Singleline);
            if (k == 0 && !named.Success)
            {
                BuildValue value = Evaluate(ParseAt(part, at), at, constant);
                if (!value.Whole || value.Long < 1 || value.Long > 1024)
                    throw new BuildProblem(at, $"a pass runs 1 to 1024 threads a group; {name} asks for {value}.");
                threads = (int)value.Long;
                continue;
            }

            if (!named.Success || named.Groups[1].Value is not ("over" or "groups"))
                throw new BuildProblem(at, "after the threads, a pass says over: n (a thread each) or groups: g (whole groups).");
            BuildExpression expression = ParseAt(named.Groups[2].Value, at);
            if (named.Groups[1].Value == "over")
                over = expression;
            else
                groups = expression;
        }

        if (threads == 0)
            throw new BuildProblem(args.Index, "say the threads a group first: [Pass(256, over: count)].");
        if ((over is null) == (groups is null))
            throw new BuildProblem(args.Index, "a pass says one of over: n (a thread each) or groups: g (whole groups).");
        return (threads, over, groups);
    }

    private static BuildParameter[] Parameters(string name, Group parameters, bool library)
    {
        string[] declared = parameters.Value.Split(',', System.StringSplitOptions.TrimEntries);
        var read = new List<BuildParameter>();
        for (int k = 0; k < declared.Length; k++)
        {
            string text = Regex.Replace(declared[k], @"\s+", " ");
            Match uint32 = Regex.Match(text, @"^uint (\w+)$");
            Match buffer = BufferParameter().Match(text);
            if (k > 0 && library && buffer.Success)
                read.Add(new BuildParameter(buffer.Groups["name"].Value, buffer.Groups["type"].Value));
            else if (uint32.Success)
            {
                if (k > 0)
                    read.Add(new BuildParameter(uint32.Groups[1].Value, null));
            }
            else
                throw new BuildProblem(parameters.Index, library
                    ? $"a library pass takes uint i (its thread), then uints and RWStructuredBuffer<T>s its calls pass it: {name}(uint i, uint n, RWStructuredBuffer<uint> keys)."
                    : $"a pass takes uint i (its thread), then up to {StackBuild.MaxArguments} uints its calls in build() pass it: {name}(uint i, uint round).");
        }

        if (read.Count(parameter => !parameter.IsBuffer) > StackBuild.MaxArguments)
            throw new BuildProblem(parameters.Index, $"a pass takes at most {StackBuild.MaxArguments} uints after i.");
        return [.. read];
    }
}
