using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>A buffer a BUILD brick declares: <c>[Buffer(size)] RWStructuredBuffer&lt;T&gt; name;</c>, kept on the GPU from frame to frame.</summary>
/// <param name="Count">Elements, from the size expression.</param>
/// <param name="Start">Where its declaration starts in the body (the attribute), for the rewrite.</param>
/// <param name="End">Just past its <c>;</c>.</param>
/// <param name="Draws">Marked <c>[Draws]</c> in place of <c>[Buffer(size)]</c>: one uint the passes write, the copies of the
/// stack's quad drawn this frame.</param>
public sealed record BuildBuffer(string Name, string Type, long Count, bool Coherent, int Start, int End, bool Draws = false);

/// <summary>
/// An image a graph declares: <c>[Image(format)] RWTexture2D&lt;T&gt; name;</c>, the target's size, kept on the GPU
/// from frame to frame. Passes read and write it; <c>present</c> shows it on the item.
/// </summary>
/// <param name="Format">Its texel format as Slang names it: rgba16f, rgba8, rgba32f, rg16f, r16f, r32f or r32ui.</param>
public sealed record BuildImage(string Name, string Format, string Type, int Start, int End);

/// <summary>
/// A pass a BUILD brick declares: <c>[Pass(threads, over: n)]</c> (a thread per element of n, the
/// extra threads of the last group turned away) or <c>[Pass(threads, groups: g)]</c> (g whole
/// groups, every thread run: what code with barriers needs), on <c>void name(uint i, uint …)</c>.
/// A library's pass (<see cref="Library"/>) is one call's use of it: the brick buffers that call
/// lends it are <see cref="Bound"/>, so each distinct lending is a pass of its own.
/// </summary>
/// <param name="Parameters">The parameters after <c>i</c>: what a schedule's call passes.</param>
/// <param name="Start">Where its <c>[Pass]</c> attribute starts in the body; <paramref name="End"/> is just past it.
/// For a library's pass as called, both are where build() first calls it.</param>
public sealed record BuildPass(string Name, int Threads, BuildExpression? Over, BuildExpression? Groups, BuildParameter[] Parameters,
    int Start, int End)
{
    /// <summary>The uint parameters after <c>i</c>, by name, in order: what the pass frame's arguments carry.</summary>
    public string[] Arguments { get; } = [.. Parameters.Where(parameter => !parameter.IsBuffer).Select(parameter => parameter.Name)];

    /// <summary>The library module it comes from; null for the brick's own.</summary>
    public StackLibrary? Library { get; init; }

    /// <summary>For a library's pass, the brick buffer (its place in <see cref="StackBuild.Buffers"/>) each buffer parameter gets, in order.</summary>
    public int[] Bound { get; init; } = [];

    /// <summary>
    /// A pixel pass (<c>[Pixels(w, h)]</c>): compute over the item's pixels on the target in tiles of w × h, called as
    /// <c>trace</c> is, with <c>ItemInput</c> and <c>Canvas</c>; null for a pass over elements or groups.
    /// </summary>
    public (int Width, int Height)? Pixels { get; init; }

    /// <summary>A pixel pass's parameters as written, for its call.</summary>
    public string Signature { get; init; } = "";

    /// <summary>What build() calls it: <c>link</c>, or <c>bvh.link</c> for a library's.</summary>
    public string Called => Library is null ? Name : $"{Library.Name}.{Name}";
}

/// <summary>One step of a <c>build()</c> schedule.</summary>
public abstract record BuildStep(int Index);

/// <summary>A pass run: <c>hist(round);</c>.</summary>
public sealed record BuildCall(int Index, int Pass, BuildExpression[] Arguments) : BuildStep(Index);

/// <summary><c>present(colour, depth);</c>: the images the item shows this frame, by their places in <see cref="StackBuild.Images"/> (depth -1 for none).</summary>
public sealed record BuildPresent(int Index, int Colour, int Depth) : BuildStep(Index);

/// <summary><c>for (uint v = start; test; update) body</c>; <paramref name="Op"/> is <c>++</c>, <c>--</c> or an assignment like <c>+=</c>.</summary>
public sealed record BuildLoop(int Index, string Variable, BuildExpression Start, BuildExpression Test, string Op, BuildExpression? By,
    BuildStep[] Body) : BuildStep(Index);

/// <summary><c>if (test) yes else no</c>.</summary>
public sealed record BuildBranch(int Index, BuildExpression Test, BuildStep[] Yes, BuildStep[] No) : BuildStep(Index);

/// <summary><c>uint n = value;</c>: a name the steps after it read.</summary>
public sealed record BuildLet(int Index, string Variable, BuildExpression Value) : BuildStep(Index);

/// <summary>
/// A library's schedule called: <c>bvh.sort(n, keys, ids, …)</c>. Its body runs with only its
/// uint parameters (<paramref name="Parameters"/>, given <paramref name="Arguments"/>) and the
/// library's constants in sight; the buffers the call lent are already bound in its steps.
/// </summary>
public sealed record BuildInline(int Index, StackLibrary Library, string Name, string[] Parameters, BuildExpression[] Arguments,
    BuildStep[] Body) : BuildStep(Index);

/// <summary>A problem in a BUILD brick at a place in the body (a character index), worded for the author.</summary>
public sealed class BuildProblem(int index, string message) : Exception(message)
{
    public int Index { get; } = index;
}

/// <summary>
/// A stack's BUILD brick (<see cref="BrickKind.Build"/>), read: the buffers it keeps, the
/// passes it declares and the <c>build()</c> schedule that orders them. The schedule never
/// runs on the GPU: the CPU walks it every frame (its loops and ifs over constants, uniforms
/// and loop variables) and each call becomes a compute pass of its own, since SDL_GPU orders
/// storage writes between passes. The wrapper places every buffer (<see cref="Rewrite"/>):
/// authors never write a binding. <c>build()</c> may also call a library's passes and
/// schedules (<see cref="StackLibrary"/>), lending them the brick's buffers by name.
/// </summary>
public sealed partial class StackBuild
{
    /// <summary>The most passes one frame's schedule may run.</summary>
    public const int MaxCalls = 1024;

    /// <summary>Where a stage that doesn't touch the images puts them: past anything bound.</summary>
    public const int UnplacedImage = 96;

    /// <summary>The most arguments a pass takes after <c>i</c>.</summary>
    public const int MaxArguments = 4;

    private const int MaxIterations = 1 << 16;

    private readonly string _code;
    private readonly BuildConstants _constants;
    private readonly (int Start, int End) _schedule;
    private readonly List<BuildPass> _passes;

    public IReadOnlyList<BuildBuffer> Buffers { get; }

    /// <summary>The images it declares, in order.</summary>
    public IReadOnlyList<BuildImage> Images { get; }

    /// <summary>Whether the schedule shows images on the item (<c>present</c>): the item then draws them, not a quad of its own.</summary>
    public bool Presents { get; private set; }

    /// <summary>The <c>[Draws]</c> buffer's place in <see cref="Buffers"/>, or -1: without one the stack's quad is drawn once.</summary>
    public int Draws { get; }

    /// <summary>The brick's passes, then each library pass as build() lends it buffers, in the order first called.</summary>
    public IReadOnlyList<BuildPass> Passes => _passes;

    public IReadOnlyList<BuildStep> Schedule { get; }

    private StackBuild(string code, (int, int) schedule)
    {
        _code = code;
        _constants = new BuildConstants(code);
        _schedule = schedule;
        List<BuildBuffer> buffers = ReadBuffers();
        Buffers = buffers;
        Images = ReadImages();
        Draws = buffers.FindIndex(buffer => buffer.Draws);
        _passes = BuildPasses.Read(code, Constant, library: false);
        Schedule = ReadSchedule();
    }

    /// <summary>The body's BUILD brick read, or null when it has no <c>build()</c>; throws <see cref="BuildProblem"/> when it is written wrong.</summary>
    public static StackBuild? Of(string source)
    {
        string code = BuildText.Blank(source);
        Match entry = BuildEntry().Match(code);
        if (!entry.Success)
        {
            Match stray = Regex.Match(code, @"\[\s*(?:(?:Buffer|Pass|Image|Pixels)\s*\(|Draws\s*\])");
            if (stray.Success)
                throw new BuildProblem(stray.Index, "[Buffer], [Image], [Draws], [Pass] and [Pixels] belong in a BUILD brick, with a void graph() that runs the passes.");
            return null;
        }

        int open = code.IndexOf('{', entry.Index + entry.Length);
        int close = BuildText.Matching(code, open);
        if (open < 0 || close < 0)
            throw new BuildProblem(entry.Index, "graph() needs a body in braces.");
        return new StackBuild(code, (entry.Index, close + 1));
    }

    /// <summary>The schedule: <c>graph()</c>, or <c>build()</c> as stacks first named it.</summary>
    [GeneratedRegex(@"\bvoid\s+(?:graph|build)\s*\(\s*\)")]
    private static partial Regex BuildEntry();

    [GeneratedRegex(@"\[\s*Image\s*\(\s*(?<format>\w+)\s*\)\s*\]\s*RWTexture2D\s*<\s*(?<type>\w+)\s*>\s*(?<name>\w+)\s*;")]
    private static partial Regex ImageDeclaration();

    /// <summary>The image formats a graph may declare, and the texel type each is read as.</summary>
    public static readonly IReadOnlyDictionary<string, string> ImageFormats = new Dictionary<string, string>
    {
        ["rgba16f"] = "float4", ["rgba8"] = "float4", ["rgba32f"] = "float4", ["rg16f"] = "float2",
        ["r16f"] = "float", ["r32f"] = "float", ["r32ui"] = "uint",
    };

    [GeneratedRegex(@"\[\s*(?:Buffer\s*\((?<size>[^\]]*)\)|(?<draws>Draws))\s*\]\s*(?<coherent>globallycoherent\s+)?RWStructuredBuffer\s*<\s*(?<type>[\w:.]+)\s*>\s*(?<name>\w+)\s*;")]
    private static partial Regex BufferDeclaration();

    /// <summary>Whether a brick's graph shows images on the item (<c>present(…)</c>): with no SHADE brick, that is all it draws.</summary>
    public static bool PresentsIn(string brick) => Regex.IsMatch(BuildText.Blank(brick), @"\bpresent\s*\(");

    /// <summary>A <c>static const</c> of the body, worked out; null when there is none by that name or it can't be.</summary>
    public BuildValue? Constant(string name) => _constants.Of(name);

    private List<BuildBuffer> ReadBuffers()
    {
        var buffers = new List<BuildBuffer>();
        var declared = BufferDeclaration().Matches(_code);
        foreach (Match marker in Regex.Matches(_code, @"\[\s*(?:Buffer\s*\(|Draws\s*\])"))
            if (!declared.Any(match => match.Index == marker.Index))
                throw new BuildProblem(marker.Index, "write a buffer as [Buffer(size)] RWStructuredBuffer<T> name; (globallycoherent may go before RWStructuredBuffer), or [Draws] RWStructuredBuffer<uint> name;.");

        foreach (Match match in declared)
        {
            string name = match.Groups["name"].Value;
            if (buffers.Any(buffer => buffer.Name == name))
                throw new BuildProblem(match.Index, $"the buffer {name} is declared twice.");
            if (match.Groups["draws"].Success)
            {
                if (match.Groups["type"].Value != "uint")
                    throw new BuildProblem(match.Index, $"a [Draws] buffer is one uint, the copies drawn: [Draws] RWStructuredBuffer<uint> {name};");
                if (buffers.Any(buffer => buffer.Draws))
                    throw new BuildProblem(match.Index, "a stack has one [Draws] buffer.");
                buffers.Add(new BuildBuffer(name, "uint", 1, match.Groups["coherent"].Success, match.Index, match.Index + match.Length, Draws: true));
                continue;
            }

            Group size = match.Groups["size"];
            BuildValue count = BuildPasses.Evaluate(BuildPasses.ParseAt(size.Value, size.Index), size.Index, Constant);
            if (!count.Whole || count.Long < 1)
                throw new BuildProblem(size.Index, $"a buffer's size is a whole number of elements, at least 1 ({name} has {count}).");
            buffers.Add(new BuildBuffer(name, match.Groups["type"].Value, count.Long, match.Groups["coherent"].Success,
                match.Index, match.Index + match.Length));
        }

        return buffers;
    }

    private List<BuildImage> ReadImages()
    {
        var images = new List<BuildImage>();
        var declared = ImageDeclaration().Matches(_code);
        foreach (Match marker in Regex.Matches(_code, @"\[\s*Image\s*\("))
            if (!declared.Any(match => match.Index == marker.Index))
                throw new BuildProblem(marker.Index, "write an image as [Image(format)] RWTexture2D<T> name; (format rgba16f, rgba8, rgba32f, rg16f, r16f, r32f or r32ui).");

        foreach (Match match in declared)
        {
            string name = match.Groups["name"].Value, format = match.Groups["format"].Value, type = match.Groups["type"].Value;
            if (!ImageFormats.TryGetValue(format, out string? texel))
                throw new BuildProblem(match.Index, $"{format} isn't an image format; use rgba16f, rgba8, rgba32f, rg16f, r16f, r32f or r32ui.");
            if (texel != type)
                throw new BuildProblem(match.Index, $"a {format} image holds {texel}: [Image({format})] RWTexture2D<{texel}> {name};");
            if (images.Any(image => image.Name == name) || Buffers.Any(buffer => buffer.Name == name))
                throw new BuildProblem(match.Index, $"{name} is declared twice.");
            images.Add(new BuildImage(name, format, type, match.Index, match.Index + match.Length));
        }

        return images;
    }

    // ---- the body as each stage compiles it --------------------------------------------

    /// <summary>
    /// The body with every buffer declared at the binding <paramref name="binding"/> gives it (by its
    /// place in <see cref="Buffers"/>) in descriptor set <paramref name="set"/>, and the <c>[Pass]</c>
    /// attributes and <c>build()</c> blanked: Slang never sees the schedule. Lines stay where they were.
    /// <c>globallycoherent</c> is left off, so a buffer can be lent to a library's function (Slang
    /// takes no coherent parameter); the compile marks the binding coherent after (<see cref="SpirvCoherence"/>).
    /// </summary>
    /// <remarks>Images go at <paramref name="image"/>'s binding in the same set, or past anything bound when it is null.</remarks>
    public string Rewrite(string source, int set, Func<int, int> binding, Func<int, int>? image = null)
    {
        var spans = new List<(int Start, int End, string Text)>();
        for (int k = 0; k < Buffers.Count; k++)
        {
            BuildBuffer buffer = Buffers[k];
            spans.Add((buffer.Start, buffer.End, $"[[vk::binding({binding(k)}, {set})]] RWStructuredBuffer<{buffer.Type}> {buffer.Name};"));
        }

        for (int k = 0; k < Images.Count; k++)
        {
            BuildImage declared = Images[k];
            int at = image?.Invoke(k) ?? UnplacedImage + k;
            spans.Add((declared.Start, declared.End,
                $"[[vk::binding({at}, {set})]] [[vk::image_format(\"{declared.Format}\")]] RWTexture2D<{declared.Type}> {declared.Name};"));
        }

        foreach (BuildPass pass in _passes.Where(pass => pass.Library is null))
            spans.Add((pass.Start, pass.End, ""));
        spans.Add((_schedule.Start, _schedule.End, ""));
        return BuildText.Replaced(source, spans);
    }
}
