using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// A list a graph declares: <c>[Size(n)] List&lt;T&gt; name;</c>, kept on the GPU from frame to frame (a
/// <c>Counter name;</c> is a list of one uint), or <c>[Host]</c> before it: the same, filled by the host from the material's buffer of that name
/// (<c>CanvasMaterial.SetBuffer</c>) whenever that changes, for the passes to read.
/// </summary>
/// <param name="Count">Elements, from the size expression.</param>
/// <param name="Start">Where its declaration starts in the body (the attribute), for the rewrite.</param>
/// <param name="End">Just past its <c>;</c>.</param>
/// <param name="Host">Marked <c>[Host]</c>: the host writes it.</param>
/// <param name="Cached">Marked <c>[Cached]</c> before it: the passes writing it run only when it goes stale (just made, or
/// the material's values moved), so a mesh built from values is built once, not every frame.</param>
/// <param name="Bundle">The <see cref="BuildBundle"/> it is a part of, by name; null for a list of its own.</param>
public sealed record BuildBuffer(string Name, string Type, long Count, bool Coherent, int Start, int End, bool Host = false, bool Cached = false,
    string? Bundle = null);

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

    /// <summary>A draw marked <c>[Translucent]</c> just before its <c>[Draw]</c>: it tests the depth and never writes it.</summary>
    public bool Translucent { get; init; }

    /// <summary>A draw marked <c>[CoversBox]</c>: its grid covers the item's box on the target instead of its quad.</summary>
    public bool CoversBox { get; init; }

    /// <summary>A draw's shade field marked <c>: SV_Depth</c>, written as the pixel's depth; null when it writes the grid's.</summary>
    public string? DepthField { get; init; }

    /// <summary>The uint buffer whose first element counts a draw's copies on the GPU (its place in the buffers), or -1.</summary>
    public int CopiesBuffer { get; init; } = -1;

    /// <summary>Where in <see cref="CopiesBuffer"/> the count sits, in uints: a bundle's place in the bookkeeping, else 0.</summary>
    public long CopiesAt { get; init; }

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

    /// <summary>The declarations that keep several of <see cref="Buffers"/> under one name, in order.</summary>
    public IReadOnlyList<BuildBundle> Bundles { get; }

    /// <summary>Some buffer is <c>[Cached]</c>: the runner asks once a frame whether they need filling.</summary>
    public bool CachesBuffers { get; }

    /// <summary>The images it declares, in order.</summary>
    public IReadOnlyList<BuildImage> Images { get; }


    /// <summary>The brick's passes, then each library pass as build() lends it buffers, in the order first called.</summary>
    public IReadOnlyList<BuildPass> Passes => _passes;

    public IReadOnlyList<BuildStep> Schedule { get; }

    private StackBuild(string code, (int, int) schedule)
    {
        _code = code;
        _constants = new BuildConstants(code);
        _schedule = schedule;
        (List<BuildBuffer> buffers, List<BuildImage> images, List<BuildBundle> bundles) = BuildDeclarations.Read(code, Constant);
        Buffers = buffers;
        Bundles = bundles;
        CachesBuffers = buffers.Exists(buffer => buffer.Cached);
        Images = images;
        _passes = BuildPasses.Read(code, Constant, library: false);
        for (int p = 0; p < _passes.Count; p++)
        {
            if (_passes[p].CopiesName is { } counted && bundles.Find(bundle => bundle.Name == counted) is { } bundle)
                _passes[p] = bundle.CountAt >= 0
                    ? _passes[p] with { Copies = null, CopiesBuffer = buffers.FindIndex(buffer => buffer.Name == BundleKinds.Bookkeeping), CopiesAt = bundle.CountAt }
                    : throw new BuildProblem(_passes[p].Start, $"{_passes[p].Name}'s copies come from {counted}, a {bundle.Type}, which has no count to draw by.");
            else if (_passes[p].CopiesName is { } named && buffers.FindIndex(buffer => buffer.Name == named) is var count and >= 0)
                _passes[p] = buffers[count].Type == "uint"
                    ? _passes[p] with { Copies = null, CopiesBuffer = count }
                    : throw new BuildProblem(_passes[p].Start, $"{_passes[p].Name}'s copies come from {named}, so it holds uints: its first is the count.");
            if (_passes[p].PixelsOver is { } over)
            {
                int image = Images.ToList().FindIndex(declared => declared.Name == over);
                if (image < 0)
                    throw new BuildProblem(_passes[p].Start, $"{_passes[p].Name} runs over {over}, which isn't an Image this graph declares.");
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
            Match stray = Regex.Match(code, @"\[\s*(?:Pass|Pixels|Draw)\s*\(");
            if (stray.Success || BuildDeclarations.Any(code))
                throw new BuildProblem(stray.Success ? stray.Index : 0, "Lists, Counters, Images, [Pass], [Pixels] and [Draw] belong to a graph: declare a void graph() that runs them, in any of the stack's files.");
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

    /// <summary>A <c>static const</c> of the body, worked out; null when there is none by that name or it can't be.</summary>
    public BuildValue? Constant(string name) => _constants.Of(name);

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
        string Declared(int k) => $"[[vk::binding({binding(k)}, {set})]] RWStructuredBuffer<{Buffers[k].Type}> {Buffers[k].Name};";
        for (int k = 0; k < Buffers.Count; k++)
            if (Buffers[k].Bundle is null)
                spans.Add((Buffers[k].Start, Buffers[k].End, Declared(k)));
        foreach (BuildBundle bundle in Bundles)
            spans.Add((bundle.Start, bundle.End, BundleKinds.Rewritten(bundle, Buffers, Declared)));

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
