using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xanadu.Core.Rendering;

namespace Xanadu.Slang;

/// <summary>
/// A pass's parameter after its thread: a number its calls pass (uint, int or float), or (in a library) a buffer the call
/// lends it, or the block it is called on.
/// </summary>
/// <param name="BufferType">The element type of a <c>List&lt;T&gt;</c> parameter; null for a number or a block.</param>
/// <param name="Scalar">A number's type: uint, int or float.</param>
/// <param name="Block">A block parameter's type (<see cref="BlockType"/>): the pass is called on a declared block.</param>
public sealed record BuildParameter(string Name, string? BufferType, string Scalar = "uint", string? Block = null)
{
    public bool IsBuffer => BufferType is not null;

    public bool IsNumber => BufferType is null && Block is null;
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

    [GeneratedRegex(@"(?<attribute>\[\s*Pixels\s*\(\s*(?<w>\d+)\s*,\s*(?<h>\d+)\s*(?:,\s*over\s*:\s*(?<over>\w+)\s*)?\)\s*\])\s*void\s+(?<name>\w+)\s*\((?<params>[^)]*)\)")]
    private static partial Regex PixelsDeclaration();

    [GeneratedRegex(@"(?<attribute>\[\s*Draw\s*\(\s*(?<w>\d+)\s*,\s*(?<h>\d+)\s*,\s*shade\s*:\s*(?<shade>\w+)\s*(?:,\s*blend\s*:\s*(?<blend>\w+)\s*)?(?:,\s*copies\s*:\s*(?<copies>[^\]]+?)\s*)?\)\s*\])\s*ItemVertex\s+(?<name>\w+)\s*\(\s*ItemVertex\s+\w+\s*\)")]
    private static partial Regex DrawDeclaration();

    [GeneratedRegex(@"(?<attribute>\[\s*MeshDraw\s*\(\s*(?<w>\d+)\s*,\s*(?<h>\d+)\s*,\s*shade\s*:\s*(?<shade>\w+)\s*(?:,\s*blend\s*:\s*(?<blend>\w+)\s*)?(?:,\s*counts\s*:\s*(?<counts>\w+)\s*)?(?:,\s*copies\s*:\s*(?<copies>[^\]]+?)\s*)?\)\s*\])\s*MeshPart\s+(?<name>\w+)\s*\(\s*MeshPart\s+\w+\s*\)")]
    private static partial Regex MeshDrawDeclaration();

    /// <summary>The most corners, and the most triangles, a mesh draw's workgroup makes.</summary>
    private const int MostMeshParts = 256;

    /// <summary>The most cells a draw's grid has along a side.</summary>
    private const int MostCells = 512;

    /// <summary>The most threads a pixel pass's tile holds.</summary>
    private const int MostTileThreads = 1024;

    [GeneratedRegex(@"^List\s*<\s*(?<type>[\w:.]+)\s*>\s+(?<name>\w+)$")]
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
    /// <param name="blocks">A library's block types: a pass may take one first, after its thread, and is called on it.</param>
    public static List<BuildPass> Read(string code, System.Func<string, BuildValue?> constant, bool library, IReadOnlyCollection<string>? blocks = null)
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
            BuildParameter[] parameters = Parameters(name, match.Groups["params"], library, blocks ?? []);
            passes.Add(new BuildPass(name, threads, over, groups, parameters, match.Index, match.Index + match.Groups["attribute"].Length));
        }

        if (!library)
        {
            ReadPixels(code, passes);
            ReadDraws(code, passes);
        }
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
                throw new BuildProblem(marker.Index, "write a pixel pass as [Pixels(8, 8)] void name(ItemInput i, Canvas canvas), or [Pixels(8, 8, over: image)] to run over an image's texels instead of the item's box.");

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
                PixelsOver = match.Groups["over"] is { Success: true } over ? over.Value : null,
                Signature = match.Groups["params"].Value,
            });
        }
    }

    /// <summary>
    /// The brick's draws: <c>[Draw(cells across, cells down, shade: fn)] ItemVertex name(ItemVertex v)</c>, a grid of
    /// triangles over the item's quad moved by <c>name</c> as a deform moves corners and coloured by <c>fn</c> as shade
    /// colours pixels, drawn into the images its call names. <c>blend: mode</c> after the shade blends it onto its
    /// targets (a <see cref="BlendMode"/> by name; without it an exit blends as its material says and an image draw
    /// overwrites). <c>copies: n</c> last draws the grid n times, each copy
    /// seeing its number as <c>copy</c>: n a number, a constant or a value, or a uint buffer whose first element the
    /// graph's passes write (drawn indirectly, the count never coming back to the CPU).
    /// </summary>
    private static void ReadDraws(string code, List<BuildPass> passes)
    {
        var declared = DrawDeclaration().Matches(code);
        foreach (Match marker in Regex.Matches(code, @"\[\s*Draw\s*\("))
            if (!declared.Any(match => match.Index == marker.Index))
                throw new BuildProblem(marker.Index, "write a draw as [Draw(64, 64, shade: name)] ItemVertex name(ItemVertex v), with float4 name(ItemInput i) for its shade, or a struct of outputs as its return type; blend: mode after the shade blends it (mix, add, sub, mul, premultiplied); copies: n last draws it n times.");
        var meshes = MeshDrawDeclaration().Matches(code);
        foreach (Match marker in Regex.Matches(code, @"\[\s*MeshDraw\s*\("))
            if (!meshes.Any(match => match.Index == marker.Index))
                throw new BuildProblem(marker.Index, "write a mesh draw as [MeshDraw(corners, triangles, shade: name)] MeshPart name(MeshPart m); blend: as a [Draw]'s, then counts: fn (uint2 fn(): this workgroup's corners and triangles, at most the draw's), then copies: as a [Draw]'s.");

        foreach (Match match in declared.Concat(meshes))
        {
            bool mesh = match.Groups["attribute"].Value.Contains("MeshDraw");
            string name = match.Groups["name"].Value;
            if (name is "build" or "graph" or "present" or "deform")
                throw new BuildProblem(match.Index, $"{name} is taken; give the draw another name.");
            if (passes.Any(pass => pass.Name == name))
                throw new BuildProblem(match.Index, $"the pass {name} is declared twice.");
            int across = int.Parse(match.Groups["w"].Value), down = int.Parse(match.Groups["h"].Value);
            if (mesh && (across < 1 || down < 1 || across > MostMeshParts || down > MostMeshParts))
                throw new BuildProblem(match.Index, $"a mesh draw makes 1 to {MostMeshParts} corners and triangles, not {across} and {down}.");
            if (!mesh && (across < 1 || down < 1 || across > MostCells || down > MostCells))
                throw new BuildProblem(match.Index, $"a draw's grid has 1 to {MostCells} cells a side, not {across} × {down}.");
            Group copies = match.Groups["copies"];
            if (Marked(code, match.Index, "Opaque") && Marked(code, match.Index, "Translucent"))
                throw new BuildProblem(match.Index, $"{name} is marked both [Opaque] (writes the depth) and [Translucent] (never writes it); keep one.");
            if (!mesh && Marked(code, match.Index, "OneSided"))
                throw new BuildProblem(match.Index, $"[OneSided] drops triangles facing away in a mesh draw; {name} is a [Draw].");
            passes.Add(new BuildPass(name, 1, null, null, [], match.Index, match.Index + match.Groups["attribute"].Length)
            {
                Mesh = mesh ? (across, down, match.Groups["counts"] is { Success: true } counts ? counts.Value : null) : null,
                Draw = (mesh ? 1 : across, mesh ? 1 : down, match.Groups["shade"].Value, DrawOutput.Of(code, match.Groups["shade"].Value, match.Index, out string? depth)),
                DepthField = depth,
                CoversBox = Marked(code, match.Index, "CoversBox"),
                Copies = copies.Success ? ParseAt(copies.Value, copies.Index) : null,
                CopiesName = copies.Success && Regex.IsMatch(copies.Value, @"^\w+$") ? copies.Value : null,
                Translucent = Marked(code, match.Index, "Translucent"),
                Opaque = Marked(code, match.Index, "Opaque"),
                OrderDepth = Marked(code, match.Index, "OrderDepth"),
                OneSided = Marked(code, match.Index, "OneSided"),
                Blend = match.Groups["blend"] is { Success: true } blend ? BlendOf(blend) : null,
            });
        }
    }

    /// <summary>A draw's <c>blend:</c>, one of <see cref="BlendMode"/>'s names in any case.</summary>
    private static BlendMode BlendOf(Group blend) =>
        System.Enum.TryParse(blend.Value, ignoreCase: true, out BlendMode mode) && System.Enum.IsDefined(mode)
            ? mode
            : throw new BuildProblem(blend.Index, $"blend: is one of mix, add, sub, mul or premultiplied, not {blend.Value}.");

    /// <summary>Whether the attributes just before <paramref name="at"/> include <c>[marker]</c>, in any order.</summary>
    private static bool Marked(string code, int at, string marker) =>
        Regex.Match(code[..at], @"(?:\[\s*\w+\s*\]\s*)*$").Value is var run && Regex.IsMatch(run, $@"\[\s*{marker}\s*\]");

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

    private static BuildParameter[] Parameters(string name, Group parameters, bool library, IReadOnlyCollection<string> blocks)
    {
        string[] declared = parameters.Value.Split(',', System.StringSplitOptions.TrimEntries);
        var read = new List<BuildParameter>();
        for (int k = 0; k < declared.Length; k++)
        {
            string text = Regex.Replace(declared[k], @"\s+", " ");
            Match number = Regex.Match(text, @"^(?<scalar>uint|int|float) (?<name>\w+)$");
            Match buffer = BufferParameter().Match(text);
            Match block = Regex.Match(text, @"^(?<type>\w+) (?<name>\w+)$");
            if (k == 0 && text.StartsWith("uint "))
                continue;
            if (k > 0 && library && buffer.Success)
                read.Add(new BuildParameter(buffer.Groups["name"].Value, buffer.Groups["type"].Value));
            else if (k > 0 && number.Success)
                read.Add(new BuildParameter(number.Groups["name"].Value, null, number.Groups["scalar"].Value));
            else if (k == 1 && block.Success && blocks.Contains(block.Groups["type"].Value))
                read.Add(new BuildParameter(block.Groups["name"].Value, null, Block: block.Groups["type"].Value));
            else
                throw new BuildProblem(parameters.Index, library
                    ? $"a library pass takes uint i (its thread), then maybe the block it is called on, then numbers (uint, int, float) and List<T>s its calls pass it: {name}(uint i, uint n, List<uint> keys)."
                    : $"a pass takes uint i (its thread), then up to {StackBuild.MaxArguments} numbers (uint, int, float) its calls in graph() pass it: {name}(uint i, uint round, float fade).");
        }

        if (read.Count(parameter => parameter.IsNumber) > StackBuild.MaxArguments)
            throw new BuildProblem(parameters.Index, $"a pass takes at most {StackBuild.MaxArguments} numbers after i.");
        return [.. read];
    }
}
