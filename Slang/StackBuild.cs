using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// A buffer a BUILD brick declares: <c>[Buffer(size)] RWStructuredBuffer&lt;T&gt; name;</c>, kept on the GPU from frame to
/// frame, or <c>[Host(size)]</c>: the same, filled by the host from the material's buffer of that name
/// (<c>CanvasMaterial.SetBuffer</c>) whenever that changes, for the passes to read.
/// </summary>
/// <param name="Count">Elements, from the size expression.</param>
/// <param name="Start">Where its declaration starts in the body (the attribute), for the rewrite.</param>
/// <param name="End">Just past its <c>;</c>.</param>
/// <param name="Draws">Marked <c>[Draws]</c> in place of <c>[Buffer(size)]</c>: one uint the passes write, the copies of the
/// stack's quad drawn this frame.</param>
/// <param name="Host">Marked <c>[Host(size)]</c>: the host writes it.</param>
public sealed record BuildBuffer(string Name, string Type, long Count, bool Coherent, int Start, int End, bool Draws = false, bool Host = false);

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

    /// <summary>A draw's <c>copies:</c>, worked out when lowered; null for one copy, or when <see cref="CopiesBuffer"/> counts them.</summary>
    public BuildExpression? Copies { get; init; }

    /// <summary>A draw's <c>copies:</c> when it is a bare name, which may be a buffer's.</summary>
    public string? CopiesName { get; init; }

    /// <summary>The uint buffer whose first element counts a draw's copies on the GPU (its place in the buffers), or -1.</summary>
    public int CopiesBuffer { get; init; } = -1;

    /// <summary>The library module it comes from; null for the brick's own.</summary>
    public StackLibrary? Library { get; init; }

    /// <summary>For a library's pass, the brick buffer (its place in <see cref="StackBuild.Buffers"/>) each buffer parameter gets, in order.</summary>
    public int[] Bound { get; init; } = [];

    /// <summary>
    /// A pixel pass (<c>[Pixels(w, h)]</c>): compute over the item's pixels on the target in tiles of w × h, called as
    /// <c>trace</c> is, with <c>ItemInput</c> and <c>Canvas</c>; null for a pass over elements or groups.
    /// </summary>
    public (int Width, int Height)? Pixels { get; init; }

    /// <summary>The image a pixel pass runs over (<c>over: name</c>), as written; null for the item's box.</summary>
    public string? PixelsOver { get; init; }

    /// <summary><see cref="PixelsOver"/>'s place in <see cref="StackBuild.Images"/>, or -1 for the item's box.</summary>
    public int OverImage { get; init; } = -1;

    /// <summary>
    /// A draw (<c>[Draw]</c>): its grid's cells, the function that shades it, and the targets that writes, from its
    /// return type; null for a compute pass.
    /// </summary>
    public (int Across, int Down, string Shade, DrawOutput[] Outputs)? Draw { get; init; }

    /// <summary>A pixel pass's parameters as written, for its call.</summary>
    public string Signature { get; init; } = "";

    /// <summary>What build() calls it: <c>link</c>, or <c>bvh.link</c> for a library's.</summary>
    public string Called => Library is null ? Name : $"{Library.Name}.{Name}";
}

/// <summary>One step of a <c>build()</c> schedule.</summary>
public abstract record BuildStep(int Index);

/// <summary>A pass run: <c>hist(round);</c>.</summary>
public sealed record BuildCall(int Index, int Pass, BuildExpression[] Arguments) : BuildStep(Index);

/// <summary>
/// A draw run into images by their places in <see cref="StackBuild.Images"/>, one for each output in order:
/// <c>vase(colour);</c>, <c>vase(depth, normals, bases);</c>; or onto the frame, <c>vase(frame);</c> (<see cref="Frame"/>).
/// </summary>
public sealed record BuildDraw(int Index, int Pass, int[] Targets) : BuildStep(Index)
{
    /// <summary>The target that is the frame: the draw is an exit.</summary>
    public const int Frame = -2;

    public bool Exits => Targets is [Frame];
}

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
/// A stack's BUILD brick (<see cref="StackRole.Graph"/>), read: the buffers it keeps, the
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
        for (int p = 0; p < _passes.Count; p++)
        {
            if (_passes[p].CopiesName is { } counted && buffers.FindIndex(buffer => buffer.Name == counted) is var count and >= 0)
                _passes[p] = buffers[count].Type == "uint"
                    ? _passes[p] with { Copies = null, CopiesBuffer = count }
                    : throw new BuildProblem(_passes[p].Start, $"{_passes[p].Name}'s copies come from {counted}, so it holds uints: its first is the count.");
            if (_passes[p].PixelsOver is { } over)
            {
                int image = Images.ToList().FindIndex(declared => declared.Name == over);
                if (image < 0)
                    throw new BuildProblem(_passes[p].Start, $"{_passes[p].Name} runs over {over}, which isn't an [Image] this graph declares.");
                _passes[p] = _passes[p] with { OverImage = image };
            }
        }

        Schedule = ReadSchedule();
    }

    /// <summary>The body's BUILD brick read, or null when it has no <c>build()</c>; throws <see cref="BuildProblem"/> when it is written wrong.</summary>
    public static StackBuild? Of(string source)
    {
        string code = BuildText.Blank(source);
        Match entry = BuildEntry().Match(code);
        if (!entry.Success)
        {
            Match stray = Regex.Match(code, @"\[\s*(?:(?:Buffer|Host|Pass|Image|History|Pixels|Draw)\s*\(|(?:Draws|Kept)\s*\])");
            if (stray.Success)
                throw new BuildProblem(stray.Index, "[Buffer], [Host], [Image], [Kept], [History], [Draws], [Pass], [Pixels] and [Draw] belong to a graph: declare a void graph() that runs them, in any of the stack's files.");
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

    [GeneratedRegex(@"(?<kept>\[\s*Kept\s*\]\s*)?(?<image>\[\s*Image\s*\(\s*(?<format>\w+)\s*(?:,\s*scale\s*:\s*(?<scale>[^,\)\]]+?)\s*)?(?:,\s*(?<w>[^,:\)\]]+?)\s*,\s*(?<h>[^,:\)\]]+?)\s*)?(?:,\s*clear\s*:\s*(?<clear>[^\)\]]+?)\s*)?\)\s*\])\s*RWTexture2D\s*<\s*(?<type>\w+)\s*>\s*(?<name>\w+)\s*;")]
    private static partial Regex ImageDeclaration();

    [GeneratedRegex(@"\[\s*History\s*\(\s*(?<source>\w+)\s*\)\s*\]\s*RWTexture2D\s*<\s*(?<type>\w+)\s*>\s*(?<name>\w+)\s*;")]
    private static partial Regex HistoryDeclaration();

    /// <summary>The image formats a graph may declare, and the texel type each is read as.</summary>
    public static readonly IReadOnlyDictionary<string, string> ImageFormats = new Dictionary<string, string>
    {
        ["rgba16f"] = "float4", ["rgba8"] = "float4", ["rgba32f"] = "float4", ["rg16f"] = "float2",
        ["r16f"] = "float", ["r32f"] = "float", ["r32ui"] = "uint",
    };

    [GeneratedRegex(@"\[\s*(?:(?:Buffer|(?<host>Host))\s*\((?<size>[^\]]*)\)|(?<draws>Draws))\s*\]\s*(?<coherent>globallycoherent\s+)?RWStructuredBuffer\s*<\s*(?<type>[\w:.]+)\s*>\s*(?<name>\w+)\s*;")]
    private static partial Regex BufferDeclaration();

    /// <summary>A <c>static const</c> of the body, worked out; null when there is none by that name or it can't be.</summary>
    public BuildValue? Constant(string name) => _constants.Of(name);

    private List<BuildBuffer> ReadBuffers()
    {
        var buffers = new List<BuildBuffer>();
        var declared = BufferDeclaration().Matches(_code);
        foreach (Match marker in Regex.Matches(_code, @"\[\s*(?:Buffer\s*\(|Host\s*\(|Draws\s*\])"))
            if (!declared.Any(match => match.Index == marker.Index))
                throw new BuildProblem(marker.Index, "write a buffer as [Buffer(size)] RWStructuredBuffer<T> name; (globallycoherent may go before RWStructuredBuffer), [Host(size)] for one the host fills, or [Draws] RWStructuredBuffer<uint> name;.");

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
                match.Index, match.Index + match.Length, Host: match.Groups["host"].Success));
        }

        return buffers;
    }

    private List<BuildImage> ReadImages()
    {
        var images = new List<BuildImage>();
        var declared = ImageDeclaration().Matches(_code);
        foreach (Match marker in Regex.Matches(_code, @"\[\s*Image\s*\("))
            if (!declared.Any(match => match.Groups["image"].Index == marker.Index))
                throw new BuildProblem(marker.Index, "write an image as [Image(format)] RWTexture2D<T> name; (the item's box), [Image(format, scale: 0.5)] (a part of it) or [Image(format, 256, 256)] (fixed), with clear: value last if wanted (format rgba16f, rgba8, rgba32f, rg16f, r16f, r32f or r32ui), [Kept] before it to keep it from frame to frame.");
        foreach (Match marker in Regex.Matches(_code, @"\[\s*Kept\s*\]"))
            if (!declared.Any(match => match.Groups["kept"].Success && match.Index == marker.Index))
                throw new BuildProblem(marker.Index, "[Kept] goes on an image: [Kept] [Image(format)] RWTexture2D<T> name;.");

        foreach (Match match in declared)
        {
            string name = match.Groups["name"].Value, format = match.Groups["format"].Value, type = match.Groups["type"].Value;
            if (!ImageFormats.TryGetValue(format, out string? texel))
                throw new BuildProblem(match.Index, $"{format} isn't an image format; use rgba16f, rgba8, rgba32f, rg16f, r16f, r32f or r32ui.");
            if (texel != type)
                throw new BuildProblem(match.Index, $"a {format} image holds {texel}: [Image({format})] RWTexture2D<{texel}> {name};");
            Declare(images, name, match.Index);
            float clear = 0f;
            if (match.Groups["clear"] is { Success: true } value)
                clear = (float)BuildPasses.Evaluate(BuildPasses.ParseAt(value.Value, value.Index), value.Index, Constant).Number;
            images.Add(new BuildImage(name, format, type, clear, match.Index, match.Index + match.Length,
                match.Groups["kept"].Success ? ImageLife.Kept : ImageLife.Frame, Size: SizeOf(match, name)));
        }

        var histories = HistoryDeclaration().Matches(_code);
        foreach (Match marker in Regex.Matches(_code, @"\[\s*History\s*\("))
            if (!histories.Any(match => match.Index == marker.Index))
                throw new BuildProblem(marker.Index, "write a history as [History(image)] RWTexture2D<T> name;: last frame's copy of an image this graph declares.");

        // After every image, so a history may come before its source.
        int sources = images.Count;
        foreach (Match match in histories)
        {
            string name = match.Groups["name"].Value, from = match.Groups["source"].Value, type = match.Groups["type"].Value;
            int source = images.FindIndex(0, sources, image => image.Name == from);
            if (source < 0)
                throw new BuildProblem(match.Index, $"{name} is the history of {from}, which isn't an [Image] this graph declares.");
            BuildImage of = images[source];
            if (of.Life == ImageLife.Kept)
                throw new BuildProblem(match.Index, $"{from} is [Kept]: it already holds last frame's texels, so it needs no history.");
            if (images.Any(image => image.Source == source))
                throw new BuildProblem(match.Index, $"{from} has a history already.");
            if (type != of.Type)
                throw new BuildProblem(match.Index, $"{from} holds {of.Type}, so its history does too: [History({from})] RWTexture2D<{of.Type}> {name};");
            Declare(images, name, match.Index);
            images.Add(new BuildImage(name, of.Format, of.Type, of.Clear, match.Index, match.Index + match.Length, ImageLife.History, source, of.Size));
        }

        return images;
    }

    /// <summary>An image's size as its declaration gives it: a scale of the box, a fixed size, or neither.</summary>
    private ImageSize SizeOf(Match match, string name)
    {
        double Number(Group group) => BuildPasses.Evaluate(BuildPasses.ParseAt(group.Value, group.Index), group.Index, Constant).Number;
        bool scaled = match.Groups["scale"].Success, sized = match.Groups["w"].Success;
        if (scaled && sized)
            throw new BuildProblem(match.Index, $"{name} has a scale or a size, not both.");
        if (scaled)
        {
            double scale = Number(match.Groups["scale"]);
            if (scale is <= 0 or > 4)
                throw new BuildProblem(match.Groups["scale"].Index, $"a scale is above 0 and at most 4 ({name} has {scale}).");
            return new ImageSize((float)scale);
        }

        if (!sized)
            return default;
        double width = Number(match.Groups["w"]), height = Number(match.Groups["h"]);
        if (width != Math.Floor(width) || height != Math.Floor(height) || width is < 1 or > 8192 || height is < 1 or > 8192)
            throw new BuildProblem(match.Groups["w"].Index, $"a fixed size is whole texels, 1 to 8192 each way ({name} is {width} × {height}).");
        return new ImageSize(0, (int)width, (int)height);
    }

    /// <summary>Throws when <paramref name="name"/> is an image or buffer already.</summary>
    private void Declare(List<BuildImage> images, string name, int at)
    {
        if (images.Any(image => image.Name == name) || Buffers.Any(buffer => buffer.Name == name))
            throw new BuildProblem(at, $"{name} is declared twice.");
    }

    // ---- the body as each stage compiles it --------------------------------------------

    /// <summary>
    /// The body with every buffer declared at the binding <paramref name="binding"/> gives it (by its
    /// place in <see cref="Buffers"/>) in descriptor set <paramref name="set"/>, and the <c>[Pass]</c>
    /// attributes and <c>build()</c> blanked: Slang never sees the schedule. Lines stay where they were.
    /// <c>globallycoherent</c> is left off, so a buffer can be lent to a library's function (Slang
    /// takes no coherent parameter); the compile marks the binding coherent after (<see cref="SpirvCoherence"/>).
    /// </summary>
    /// <remarks>
    /// Images go at <paramref name="image"/>'s binding in the same set (or <paramref name="imageSet"/>), or past anything
    /// bound when it is null.
    /// </remarks>
    public string Rewrite(string source, int set, Func<int, int> binding, Func<int, int>? image = null, int? imageSet = null)
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
                $"[[vk::binding({at}, {imageSet ?? set})]] [[vk::image_format(\"{declared.Format}\")]] RWTexture2D<{declared.Type}> {declared.Name};"));
        }

        foreach (BuildPass pass in _passes.Where(pass => pass.Library is null))
            spans.Add((pass.Start, pass.End, ""));
        spans.Add((_schedule.Start, _schedule.End, ""));
        return BuildText.Replaced(source, spans);
    }
}
