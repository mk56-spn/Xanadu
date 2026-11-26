using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Slang;

/// <summary>
/// A BUILD brick's pass, compiled: its compute stage, the stack buffers it binds (by their place in
/// <see cref="StackBuild.Buffers"/>), and how it really uses each (read, write or both), in the same order.
/// </summary>
public sealed record CompiledPass(string Name, CompiledShader Shader, int[] Buffers, int Threads, GraphAccess[] Access)
{
    /// <summary>The graph's images it binds (by their place in <see cref="StackBuild.Images"/>), read-write before its buffers.</summary>
    public int[] Images { get; init; } = [];

    /// <summary>What its code does with each of <see cref="Images"/>, in the same order: read, write or both.</summary>
    public GraphAccess[] ImageAccess { get; init; } = [];

    /// <summary>
    /// The images a pixel pass reads through a sampler (<c>sampled</c>), in the order of their samplers after the canvas's
    /// own two: read only, and never among <see cref="Images"/>.
    /// </summary>
    public int[] Sampled { get; init; } = [];

    /// <summary>A pixel pass's tile; null for a pass over elements or groups.</summary>
    public (int Width, int Height)? Tile { get; init; }

    /// <summary>The image a pixel pass runs over, a thread a texel; -1 for the item's box.</summary>
    public int OverImage { get; init; } = -1;

    /// <summary>A pixel pass reads the canvas depth or the screen copy: the canvas drawn so far, so it runs mid-frame.</summary>
    public bool ReadsDepth { get; init; }

    /// <inheritdoc cref="ReadsDepth"/>
    public bool ReadsScreen { get; init; }

    /// <summary>A draw's stages; null for a compute pass.</summary>
    public CompiledDraw? Drawn { get; init; }
}

/// <summary>
/// A stack's BUILD brick, compiled: the brick as read (its schedule runs on the CPU), each
/// buffer's bytes, and every pass. Set on the fragment.
/// </summary>
public sealed record CompiledBuild(StackBuild Shape, long[] Bytes, CompiledPass[] Passes)
{
    /// <summary>
    /// By image: whether it is the material's, one for every item showing it, rather than each item's. An image a
    /// <c>[Pass]</c> touches is (the pass runs once for the material), and so is its history or source.
    /// </summary>
    public bool[] MaterialImages { get; init; } = [];

    /// <summary>Whether what the graph draws is all the stack draws: no shade, so no quad of its own after it.</summary>
    public bool Alone { get; init; }

    /// <summary>The <c>[Shared]</c> lists and images it writes and reads: what orders it against other materials.</summary>
    public SharedUse Shared { get; init; } = SharedUse.None;
}

/// <summary>
/// The graph (<see cref="StackRole.Graph"/>): buffers kept on the GPU and compute passes
/// over them, run in the order <c>graph()</c> lists. Every stage compiles the whole body, so
/// every stage declares every buffer; each binds only the ones it really reads (a first compile
/// says which, a second places those after its own buffers and the rest where nothing looks).
/// A pass's bindings: the globals read-only in set 0; its buffers read-write
/// in set 1; the pass's frame (time, the elements for <c>over:</c>, the call's arguments) at
/// uniform 0 and the values at uniform 1 of set 2.
/// </summary>
public static partial class SlangShaders
{
    /// <summary>Where a [Pass] would find the canvas depth: nowhere bound, so reading it is an error.</summary>
    private const int PassDepth = 31;

    /// <summary>The most bytes one stack buffer may take.</summary>
    public const long MaxBufferBytes = 1L << 28;

    /// <summary>SDL_GPU's cap on storage buffers a graphics stage reads, and on a compute pass's read-write ones.</summary>
    public const int MaxStageBuffers = 8;

    /// <summary>Where a stage puts the buffers it doesn't read: past anything bound, so the dead code naming them still compiles.</summary>
    private const int Unplaced = 64;

    /// <summary>The body's BUILD brick, read; null without one. Its problems throw worded as Slang's errors are.</summary>
    private static StackBuild? BuildOf(string name, string source)
    {
        try
        {
            return StackBuild.Of(source);
        }
        catch (BuildProblem problem)
        {
            throw new InvalidOperationException($"{Where(name, source, problem.Index)}: error: {problem.Message}");
        }
    }

    /// <summary>
    /// One stage compiled with the stack's buffers placed in <paramref name="set"/> from
    /// <paramref name="first"/>: only the ones it reads, in their order. Without a BUILD brick, just compiled.
    /// </summary>
    private static (CompiledShader Shader, int[] Used) Placed(StackBuild? build, string source, int set, int first, Func<string, CompiledShader> compile)
    {
        (CompiledShader shader, int[] used, _) = PlacedWithAccess(build, source, set, first, compile);
        return (shader, used);
    }

    /// <summary>
    /// <see cref="Placed"/>, with how the stage really uses each buffer it binds (in the order of
    /// <c>Used</c>): for compute, which binds them read-write whatever it does with them.
    /// </summary>
    private static (CompiledShader Shader, int[] Used, GraphAccess[] Access) PlacedWithAccess(
        StackBuild? build, string source, int set, int first, Func<string, CompiledShader> compile)
    {
        if (build is null)
            return (compile(source), [], []);
        int count = build.Buffers.Count;
        CompiledShader shader = compile(build.Rewrite(source, set, k => first + k));
        int[] used = [.. Enumerable.Range(0, count).Where(k => SpirvScan.Binds(shader.Spirv, set, first + k))];
        if (used.Length != count)
            shader = compile(build.Rewrite(source, set, k => Array.IndexOf(used, k) is var at and >= 0 ? first + at : Unplaced + k));
        int[] coherent = [.. used.Select((k, at) => (k, at)).Where(pair => build.Buffers[pair.k].Coherent).Select(pair => first + pair.at)];
        shader = SpirvCoherence.Applied(shader, set, coherent);
        return (shader, used, [.. used.Select((_, at) => SpirvScan.Access(shader.Spirv, set, first + at))]);
    }

    /// <summary>
    /// The body with every buffer placed where nothing looks: compiled once to see which a stage reads
    /// before placing them (a draw's stages).
    /// </summary>
    private static string Unbound(StackBuild? build, string source) => build?.Rewrite(source, 1, k => Unplaced + k) ?? source;

    /// <summary>Throws when a graphics stage reads more storage buffers than SDL_GPU binds.</summary>
    private static void CheckStageBuffers(string name, string stage, int bound, StackBuild? build, int[] used)
    {
        if (bound <= MaxStageBuffers)
            return;
        string buffers = build is null ? "" : $" (of the stack's: {string.Join(", ", used.Select(k => build.Buffers[k].Name))})";
        throw new InvalidOperationException(
            $"{name}: {stage} reads {bound} storage buffers{buffers}, counting the library's {Globals.Count} globals; SDL_GPU binds {MaxStageBuffers}. Share a buffer between arrays.");
    }

    /// <summary>The BUILD brick compiled: each pass's compute stage, each buffer's size from the fragment's reflection.</summary>
    private static CompiledBuild CompileBuild(string name, string source, StackBuild build, CompiledShader fragment)
    {
        var bytes = new long[build.Buffers.Count];
        for (int k = 0; k < bytes.Length; k++)
        {
            BuildBuffer buffer = build.Buffers[k];
            if (!fragment.BufferBytes.TryGetValue(buffer.Name, out int element) || element <= 0)
                throw new InvalidOperationException($"{Where(name, source, buffer.Start)}: error: {buffer.Type} has no size Slang reports.");
            bytes[k] = element * buffer.Count;
            if (bytes[k] > MaxBufferBytes)
                throw new InvalidOperationException(
                    $"{Where(name, source, buffer.Start)}: error: {buffer.Name} takes {bytes[k]:N0} bytes; a buffer takes at most {MaxBufferBytes:N0}.");
        }

        var passes = new CompiledPass[build.Passes.Count];
        for (int p = 0; p < passes.Length; p++)
        {
            BuildPass pass = build.Passes[p];
            if (pass.Pixels is not null)
            {
                passes[p] = CompilePixels(name, source, build, p);
                continue;
            }

            if (pass.Draw is not null)
            {
                passes[p] = CompileDraw(name, source, build, p);
                continue;
            }

            (CompiledShader compute, int[] images, int[] used, GraphAccess[] access) = PlacedPass(name, source, build, placed =>
                CompileWrapped(WrapPass(name, placed, build, pass), "xanaduPass", "compute", 0, 2,
                    [.. GlobalDefines(0, 0), $"XANADU_DEPTH_BINDING={PassDepth}", "XANADU_DEPTH_SET=0"]));
            CheckDeclared(name, compute, 0, 0);
            if (SpirvScan.Binds(compute.Spirv, 0, PassDepth))
                throw new InvalidOperationException($"{Where(name, source, pass.Start)}: error: a [Pass] can't read the canvas (sampleDepth): it runs once for the material; a [Pixels] pass can.");
            // A [Pass] runs once for the material, so the images it touches are the material's: they need a size of their own.
            foreach (int image in images)
                if (!build.Images[image].Size.Fixed && !build.Images[image].Screen)
                    throw new InvalidOperationException(
                        $"{Where(name, source, pass.Start)}: error: {pass.Name} runs once for the material, and {build.Images[image].Name} is sized by each item's box; give it a fixed size ([Size(w, h)]) to make it the material's, or touch it from a [Pixels] pass or a [Draw].");
            if (used.Length > MaxStageBuffers)
                throw new InvalidOperationException(
                    $"{Where(name, source, pass.Start)}: error: {pass.Name} writes {used.Length} buffers; SDL_GPU binds {MaxStageBuffers} a pass. Share a buffer between arrays.");
            passes[p] = new CompiledPass(Named(build, p),
                new CompiledShader(compute.Spirv, compute.Samplers, compute.UniformBuffers, compute.Params, compute.ParamsSize,
                    storageBuffers: Globals.Count),
                used, pass.Threads, access) { Images = images, ImageAccess = ImageAccess(compute, images) };
        }

        // A view is the canvas the host drew; a write would be lost to the next frame's draw, or race the reads.
        for (int p = 0; p < passes.Length; p++)
            for (int at = 0; at < passes[p].ImageAccess.Length; at++)
                if (passes[p] is var pass && build.Images[pass.Images[at]].View && pass.ImageAccess[at].HasFlag(GraphAccess.Write))
                    throw new InvalidOperationException($"{Where(name, source, build.Passes[p].Start)}: error: "
                        + $"{pass.Name} writes {build.Images[pass.Images[at]].Name}, a [View]: the host draws it from the canvas, so the graph only reads it.");

        var compiled = new CompiledBuild(build, bytes, passes)
        {
            MaterialImages = MaterialImages(build, passes), Alone = source.Contains(ShaderStack.GraphAlone, StringComparison.Ordinal),
            Shared = SharedUse.Of(build, passes),
        };
        // Walked once with the defaults: a schedule that can't run says so now, not every frame.
        try
        {
            build.Walk(uniform => fragment.Params.FirstOrDefault(param => param.Name == uniform) is { } param
                ? new BuildValue(param.Default.X, param.IsInt)
                : null, []);
        }
        catch (BuildProblem problem)
        {
            throw new InvalidOperationException($"{Where(name, source, problem.Index)}: error: {problem.Message}");
        }

        return compiled;
    }

    /// <summary>
    /// The images a <c>[Pass]</c> touches, with each one's history or source: the material's
    /// (<see cref="CompiledBuild.MaterialImages"/>). A <c>[Shared]</c> or <c>[View]</c> one is the frame's instead.
    /// </summary>
    private static bool[] MaterialImages(StackBuild build, CompiledPass[] passes)
    {
        var material = new bool[build.Images.Count];
        foreach (CompiledPass pass in passes)
            if (pass.Tile is null && pass.Drawn is null)
                foreach (int image in pass.Images)
                    material[image] = !build.Images[image].Screen;
        for (int k = 0; k < material.Length; k++)
            if (build.Images[k].Source is >= 0 and var source && (material[k] || material[source]))
                material[k] = material[source] = true;
        return material;
    }

    /// <summary>What a pass's code does with the images it binds, at bindings 0.. of set 1; unknown counts as both.</summary>
    private static GraphAccess[] ImageAccess(CompiledShader pass, int[] images) =>
        [.. images.Select((_, at) => SpirvScan.Access(pass.Spirv, 1, at, image: true) is var used and not 0 ? used : GraphAccess.ReadWrite)];

    /// <summary>What the board and the timings call pass <paramref name="p"/>: a library pass lent other buffers a second time is <c>sort.hist 2</c>.</summary>
    private static string Named(StackBuild build, int p)
    {
        BuildPass pass = build.Passes[p];
        int before = build.Passes.Take(p).Count(other => other.Library is not null && other.Called == pass.Called);
        return before == 0 ? pass.Called : $"{pass.Called} {before + 1}";
    }

    /// <summary>
    /// One pass as a compute entry: thread <c>i</c> of the dispatch, turned away past <c>over:</c>,
    /// with <c>dispatch</c> saying its place in its group and the call's arguments after <c>i</c>.
    /// </summary>
    private static string WrapPass(string name, string source, StackBuild build, BuildPass pass)
    {
        (string imports, string code, string label) = Split(name, source);
        string guard = pass.Over is null ? "" : "if (id.x >= xanaduPassFrame.over) return;";
        // A brick's pass takes its uints; a library's takes them and the brick buffers the call lent, in the order declared.
        int number = 0, lent = 0;
        string arguments = string.Concat(pass.Parameters.Select(parameter => parameter.IsBuffer
            ? $", effect0::{build.Buffers[pass.Bound[lent++]].Name}"
            : $", xanaduPassFrame.args[{number / 4}][{number++ % 4}]"));
        string called = pass.Library is { } library ? $"{library.Name}.{pass.Name}" : $"effect0::{pass.Name}";
        string module = pass.Library is { } from ? $"import {from.Module};" : "";
        return $$"""
            import xanadu.stage;
            {{module}}
            {{imports}}
            namespace effect0 {
            #line 1 "{{label}}"
            {{code}}
            #line 1 "{{label}} (wrapper)"
            }
            #line 1 "{{label}} (wrapper)"
            struct XanaduPassFrame { float dt; float time; uint over; float beat; uint4 args[4]; };
            [[vk::binding(0, 2)]] ConstantBuffer<XanaduPassFrame> xanaduPassFrame;
            [shader("compute")]
            [numthreads({{pass.Threads}}, 1, 1)]
            void xanaduPass(uint3 id : SV_DispatchThreadID, uint3 group : SV_GroupID, uint3 local : SV_GroupThreadID)
            {
                {{Clocks("xanaduPassFrame.time", "xanaduPassFrame.beat", "xanaduPassFrame.dt")}}
                {{TimelineClock(source, shading: false)}}
                dispatch.thread = local.x;
                dispatch.group = group.x;
                {{guard}}
                {{called}}(id.x{{arguments}});
            }
            """;
    }
}
