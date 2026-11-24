using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Slang;

/// <summary>
/// A BUILD brick's pass, compiled: its compute stage, the stack buffers it binds (by their place in
/// <see cref="StackBuild.Buffers"/>), and how it really uses each (read, write or both), in the same order.
/// </summary>
public sealed record CompiledPass(string Name, CompiledShader Shader, int[] Buffers, int Threads, GraphAccess[] Access);

/// <summary>
/// A stack's BUILD brick, compiled: the brick as read (its schedule runs on the CPU), each
/// buffer's bytes, and every pass. Set on the fragment.
/// </summary>
public sealed record CompiledBuild(StackBuild Shape, long[] Bytes, CompiledPass[] Passes);

/// <summary>
/// The BUILD brick (<see cref="BrickKind.Build"/>): buffers kept on the GPU and compute passes
/// over them, run in the order <c>build()</c> lists. Every stage compiles the whole body, so
/// every stage declares every buffer; each binds only the ones it really reads (a first compile
/// says which, a second places those after its own buffers and the rest where nothing looks).
/// A pass's bindings: the globals, then kept state, read-only in set 0; its buffers read-write
/// in set 1; the pass's frame (time, the elements for <c>over:</c>, the call's arguments) at
/// uniform 0 and the values at uniform 1 of set 2.
/// </summary>
public static partial class SlangShaders
{
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
        return (shader, used, [.. used.Select((_, at) => SpirvScan.Access(shader.Spirv, set, first + at))]);
    }

    /// <summary>
    /// The body as a stage that binds none of the stack's buffers compiles it (a pool's step, an
    /// update): every buffer placed where nothing looks. Throws, after compiling, when it reads one anyway.
    /// </summary>
    private static string Unbound(StackBuild? build, string source) => build?.Rewrite(source, 1, k => Unplaced + k) ?? source;

    /// <summary>Throws when a stage compiled from <see cref="Unbound"/> reads a stack buffer: only passes, deform and shade can.</summary>
    private static void CheckUnbound(string name, string stage, StackBuild? build, CompiledShader compiled)
    {
        if (build is null)
            return;
        for (int k = 0; k < build.Buffers.Count; k++)
            if (SpirvScan.Binds(compiled.Spirv, 1, Unplaced + k))
                throw new InvalidOperationException(
                    $"{name}: {stage} runs before the BUILD brick's passes and can't read {build.Buffers[k].Name}; only the passes, deform and shade can.");
    }

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
    private static CompiledBuild CompileBuild(string name, string source, StackBuild build, CompiledShader fragment, bool keepsState, PoolShape? pool)
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
            (CompiledShader compute, int[] used, GraphAccess[] access) = PlacedWithAccess(build, source, 1, 0, placed =>
                CompileWrapped(WrapPass(name, placed, pass, keepsState, pool), "xanaduPass", "compute", 0, 2,
                    [.. GlobalDefines(0, 0), $"XANADU_DEPTH_BINDING={ComputeDepth}", "XANADU_DEPTH_SET=0"]));
            CheckDeclared(name, compute, 0, 0);
            if (SpirvScan.Binds(compute.Spirv, 0, ComputeDepth))
                throw new InvalidOperationException($"{Where(name, source, pass.Start)}: error: a pass can't read the depth (sampleDepth); only shade and deform can.");
            if (used.Length > MaxStageBuffers)
                throw new InvalidOperationException(
                    $"{Where(name, source, pass.Start)}: error: {pass.Name} writes {used.Length} buffers; SDL_GPU binds {MaxStageBuffers} a pass. Share a buffer between arrays.");
            passes[p] = new CompiledPass(pass.Name,
                new CompiledShader(compute.Spirv, compute.Samplers, compute.UniformBuffers, compute.Params, compute.ParamsSize,
                    storageBuffers: Globals.Count + (keepsState ? 1 : 0)),
                used, pass.Threads, access);
        }

        var compiled = new CompiledBuild(build, bytes, passes);
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
    /// One pass as a compute entry: thread <c>i</c> of the dispatch, turned away past <c>over:</c>,
    /// with <c>dispatch</c> saying its place in its group and the call's arguments after <c>i</c>.
    /// </summary>
    private static string WrapPass(string name, string source, BuildPass pass, bool keepsState, PoolShape? pool)
    {
        (string imports, string code, string label) = Split(name, source);
        string clock = DrivesClock(source) ? $"time = effect0::{ClockOverride}.x; beat = effect0::{ClockOverride}.y;" : "";
        string state = keepsState
            ? $"[[vk::binding({Globals.Count}, 0)]] StructuredBuffer<State> xanaduStateIn;\nstatic State state;"
            : "";
        string load = keepsState ? "effect0::state = effect0::xanaduStateIn[0];" : "";
        string guard = pass.Over is null ? "" : "if (id.x >= xanaduPassFrame.over) return;";
        string arguments = string.Concat(pass.Arguments.Select((_, k) => $", xanaduPassFrame.args[{k}]"));
        return $$"""
            import xanadu.stage;
            {{imports}}
            namespace effect0 {
            #line 1 "{{label}}"
            {{code}}
            #line 1 "{{label}} (wrapper)"
            {{state}}
            {{ItemDeclaration(pool)}}
            }
            #line 1 "{{label}} (wrapper)"
            struct XanaduPassFrame { float dt; float time; uint over; uint pad; uint4 args; };
            [[vk::binding(0, 2)]] ConstantBuffer<XanaduPassFrame> xanaduPassFrame;
            [shader("compute")]
            [numthreads({{pass.Threads}}, 1, 1)]
            void xanaduPass(uint3 id : SV_DispatchThreadID, uint3 group : SV_GroupID, uint3 local : SV_GroupThreadID)
            {
                time = xanaduPassFrame.time;
                dt = xanaduPassFrame.dt;
                {{clock}}
                {{load}}
                effect0::dispatch.thread = local.x;
                effect0::dispatch.group = group.x;
                {{guard}}
                effect0::{{pass.Name}}(id.x{{arguments}});
            }
            """;
    }
}
