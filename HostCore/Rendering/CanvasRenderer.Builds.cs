using System.Collections.Generic;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Xanadu.Host.Rendering;

/// <summary>What a BUILD pass reads at uniform 0: the frame's clock, the elements for <c>over:</c>, and the call's arguments.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PassUniforms
{
    public float Dt, Time;
    public uint Over, Pad, A, B, C, D;
}

/// <summary>
/// Stacks with a BUILD brick: each material drawn this frame keeps its own buffers (zeroed when
/// the effect compiles), and every frame, after the pools step and before any pass draws, the
/// CPU walks the brick's <c>build()</c> and dispatches each call as a compute pass of its own,
/// so a pass sees all the passes before it wrote (SDL_GPU orders storage writes only between
/// passes). Each pass binds just the buffers it touches; deform and shade bind theirs at draw.
/// A stack with a <c>[Draws]</c> buffer is drawn indirectly: its step's args (the quad's
/// vertices, where they start) go up from the CPU and the copies are copied in from that
/// buffer on the GPU, so the count the passes wrote this frame is the count drawn.
/// </summary>
internal sealed partial class CanvasRenderer
{
    /// <summary>A material's BUILD buffers, made for one compile of its effect.</summary>
    private sealed class BuildRun(CanvasEffect effect, Buffer[] buffers) : IDisposable
    {
        public readonly CanvasEffect Effect = effect;
        public readonly Buffer[] Buffers = buffers;

        public void Dispose()
        {
            foreach (Buffer buffer in Buffers)
                buffer.Dispose();
        }
    }

    /// <summary>Bytes of zeros a new BUILD buffer is cleared from, a piece at a time.</summary>
    private const uint ZeroPiece = 1 << 20;

    private readonly Dictionary<CanvasMaterial, BuildRun> _builds = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<CanvasMaterial> _built = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<CanvasEffect> _brokenSchedules = new(ReferenceEqualityComparer.Instance);
    private readonly List<StackBuild.Run> _calls = [];
    private TransferBuffer? _zeroPiece;

    /// <summary>This frame's indirect draws: a step's place in the steps, to its args' place in <see cref="_drawArgs"/>.</summary>
    private readonly Dictionary<int, uint> _drawSlots = [];
    private Buffer? _drawArgs;
    private TransferBuffer? _drawUpload;

    /// <summary>Bytes of one draw's args: vertices, instances, first vertex, first instance.</summary>
    private const uint DrawArgsBytes = 16;

    /// <summary>
    /// The most vertices one copy of a <c>[Draws]</c> stack should take before the console says so:
    /// the copies are counted on the GPU, so the vertices drawn are this times a count the CPU never
    /// sees. A quad is 6; a grid or a fine mesh under a stack of copies is thousands (XS009).
    /// </summary>
    public const uint MostCopyVertices = 96;

    private readonly HashSet<CanvasEffect> _heavyCopies = new(ReferenceEqualityComparer.Instance);

    private void RunBuilds(CommandBuffer commands, float dt, float time)
    {
        _built.Clear();
        foreach (Step step in _steps)
        {
            if (step.Material is not { } material || !_built.Add(material))
                continue;
            CanvasEffect effect = _pipelines.Effect(material);
            if (effect.Compiled.Build is not { } build)
                continue;

            if (!_builds.TryGetValue(material, out BuildRun? run) || run.Effect != effect)
            {
                run?.Dispose();
                run = new BuildRun(effect, MakeBuffers(commands, build));
                _builds[material] = run;
            }

            _calls.Clear();
            try
            {
                build.Shape.Walk(name => Uniform(effect.Compiled, material, name), _calls);
            }
            catch (BuildProblem problem)
            {
                // Its values made the schedule impossible (a loop that won't end): said once, nothing run.
                if (_brokenSchedules.Add(effect))
                    Console.Error.WriteLine($"Shader stack {material.Shader}: build() can't run: {problem.Message}");
                continue;
            }

            Buffer? state = _states.GetValueOrDefault(material);
            foreach (StackBuild.Run call in _calls)
                Dispatch(commands, effect, run, build.Passes[call.Pass], call, state, material, dt, time);
        }
    }

    private void Dispatch(CommandBuffer commands, CanvasEffect effect, BuildRun run, CompiledPass pass, StackBuild.Run call,
        Buffer? state, CanvasMaterial material, float dt, float time)
    {
        if (call.Groups == 0)
            return;
        using GpuScope scope = commands.Scope(pass.Name);
        commands.PushComputeUniformData(new PassUniforms
        {
            Dt = dt, Time = time, Over = call.Over, A = call.A, B = call.B, C = call.C, D = call.D,
        });
        if (pass.Shader.ParamsSize > 0 && pass.Shader.UniformBuffers > 1)
            PushParams(commands, pass.Shader, material, null, stage: ParamsStage.Compute);

        Span<StorageBufferReadWriteBinding> writes = stackalloc StorageBufferReadWriteBinding[pass.Buffers.Length];
        for (int k = 0; k < writes.Length; k++)
            writes[k] = new StorageBufferReadWriteBinding(run.Buffers[pass.Buffers[k]], cycle: false);
        ComputePass compute = commands.BeginComputePass([], writes);
        compute.BindComputePipeline(effect.Passes[call.Pass]);
        if (pass.Shader.StorageBuffers > 0)
            compute.BindStorageBuffers(Bound(pass.Shader.StorageBuffers, state));
        compute.Dispatch(Math.Min(call.Groups, 65535u), 1, 1);
        commands.EndComputePass(compute);
    }

    /// <summary>
    /// Every step of a <c>[Draws]</c> stack gets args of its own: its vertices and where they
    /// start from the CPU, its copies from the stack's buffer, copied after the passes wrote it.
    /// </summary>
    private void FillDraws(CommandBuffer commands)
    {
        _drawSlots.Clear();
        for (int s = 0; s < _steps.Count; s++)
            if (Drawn(_steps[s]) is not null)
                _drawSlots[s] = (uint)_drawSlots.Count;
        if (_drawSlots.Count == 0)
            return;

        uint bytes = (uint)_drawSlots.Count * DrawArgsBytes;
        if (_drawArgs is null || _drawArgs.Size < bytes)
        {
            _drawArgs?.Dispose();
            _drawUpload?.Dispose();
            uint size = Math.Max(bytes * 2, 64 * DrawArgsBytes);
            _drawArgs = Buffer.Create<byte>(_device, "stack draws", BufferUsageFlags.Indirect, size);
            _drawUpload = TransferBuffer.Create<byte>(_device, "stack draws up", TransferBufferUsage.Upload, size);
        }

        Span<uint> args = _drawUpload!.Map<uint>(cycle: true);
        foreach ((int s, uint slot) in _drawSlots)
        {
            Step step = _steps[s];
            if (step.Count > MostCopyVertices && _heavyCopies.Add(_pipelines.Effect(step.Material)))
                Console.Error.WriteLine($"Shader stack {step.Material!.Shader}: each copy it draws is {step.Count} vertices " +
                    $"(over {MostCopyVertices}), times every copy; draw it on one plain quad, with no [Grid] (XS009).");
            args[(int)slot * 4] = step.Count;
            args[(int)slot * 4 + 1] = 0;
            args[(int)slot * 4 + 2] = step.Start;
            args[(int)slot * 4 + 3] = 0;
        }

        _drawUpload.Unmap();
        CopyPass copy = commands.BeginCopyPass();
        copy.UploadToBuffer(_drawUpload, new BufferRegion(_drawArgs, 0, bytes), cycle: false);
        foreach ((int s, uint slot) in _drawSlots)
            copy.CopyBufferToBuffer(new BufferLocation { Buffer = Drawn(_steps[s])!.Handle },
                new BufferLocation { Buffer = _drawArgs.Handle, Offset = slot * DrawArgsBytes + 4 }, 4, cycle: false);
        commands.EndCopyPass(copy);
    }

    /// <summary>The <c>[Draws]</c> buffer a step's stack draws by, or null when it draws a fixed count.</summary>
    private Buffer? Drawn(Step step)
    {
        if (step.Kind != StepKind.Effect || step.Material is not { } material)
            return null;
        CanvasEffect effect = _pipelines.Effect(material);
        return effect.Compiled.Build is { Shape.Draws: >= 0 and var draws } && BuildBuffers(material) is { } built ? built[draws] : null;
    }

    /// <summary>A uniform's value as the schedule reads it: the material's, else its default; null for a name that isn't one.</summary>
    private static BuildValue? Uniform(CompiledShader compiled, CanvasMaterial material, string name)
    {
        foreach (ShaderParam param in compiled.Params)
        {
            if (param.Name != name)
                continue;
            Vector4 value = material.Values.TryGetValue(name, out Vector4 set) ? set : param.Default;
            return param.IsInt ? BuildValue.Of((long)MathF.Round(value.X)) : new BuildValue(value.X, false);
        }

        return null;
    }

    /// <summary>The brick's buffers, each cleared to zeros.</summary>
    private Buffer[] MakeBuffers(CommandBuffer commands, CompiledBuild build)
    {
        if (_zeroPiece is null)
        {
            _zeroPiece = TransferBuffer.Create<byte>(_device, "build zeros", TransferBufferUsage.Upload, ZeroPiece);
            _zeroPiece.Map<byte>(cycle: false).Clear();
            _zeroPiece.Unmap();
        }

        var buffers = new Buffer[build.Bytes.Length];
        CopyPass copy = commands.BeginCopyPass();
        for (int k = 0; k < buffers.Length; k++)
        {
            uint bytes = (uint)((build.Bytes[k] + 3) / 4 * 4);
            buffers[k] = Buffer.Create<byte>(_device, $"stack {build.Shape.Buffers[k].Name}",
                BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead, bytes);
            for (uint at = 0; at < bytes; at += ZeroPiece)
                copy.UploadToBuffer<byte>(_zeroPiece, buffers[k], 0, at, Math.Min(ZeroPiece, bytes - at), cycle: false);
        }

        commands.EndCopyPass(copy);
        return buffers;
    }

    /// <summary>A material's BUILD buffers, or null when it keeps none (yet).</summary>
    private Buffer[]? BuildBuffers(CanvasMaterial? material) =>
        material is not null && _builds.TryGetValue(material, out BuildRun? run) ? run.Buffers : null;

    private void ForgetBuild(CanvasMaterial material)
    {
        if (_builds.Remove(material, out BuildRun? run))
            run.Dispose();
    }

    private void DisposeBuilds()
    {
        foreach (BuildRun run in _builds.Values)
            run.Dispose();
        _builds.Clear();
        _zeroPiece?.Dispose();
        _drawArgs?.Dispose();
        _drawUpload?.Dispose();
    }
}
