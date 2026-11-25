using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Rendering.Frame;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;
using Entity = Friflo.Engine.ECS.Entity;
using Texture = MoonWorks.Graphics.Texture;

namespace Xanadu.Rendering.Passes;

/// <summary>What a BUILD pass reads at uniform 0: the frame's clock and beat, the elements for <c>over:</c>, and the call's arguments.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PassUniforms
{
    public float Dt, Time;
    public uint Over;
    public float Beat;
    public uint A, B, C, D;
}

/// <summary>
/// One material's pool step: where its items start in the buffer, whether they are made afresh
/// this run, and for a pool born at a rate, how many are born this frame, the number of the
/// first, and which of the two birth counters this frame uses.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PoolJob
{
    public uint First, Reset, Births, Born, Parity, Pad0, Pad1, Pad2;
}

/// <summary>
/// The compute passes of the frame's shader stacks: every stack drawn runs the passes of its
/// render graph (<see cref="StackGraph"/>) once, in the graph's order, before any pass draws.
/// </summary>
/// <remarks>
/// Pooled stacks (a STEP brick): each material drawn this frame keeps its own
/// buffer of items, stepped once a frame by the effect's <c>ComputeOperation</c>
/// before any pass draws, then drawn once per item (the vertex and fragment
/// entries read the one they draw by the instance). A material whose effect
/// compiled again gets a fresh buffer, made by the stack's <c>start</c>. A pool
/// born at a rate is told each frame how many births are due: the rate times
/// the frame's time, the fraction carried to the next, so the rate comes out exact.
/// <para>
/// Stacks with a BUILD brick: each material drawn this frame keeps its own buffers (zeroed when
/// the effect compiles), and every frame, after the pools step and before any pass draws, the
/// CPU walks the brick's <c>build()</c> and dispatches each call as a compute pass of its own,
/// so a pass sees all the passes before it wrote (SDL_GPU orders storage writes only between
/// passes). Each pass binds just the buffers it touches; deform and shade bind theirs at draw.
/// A stack with a <c>[Draws]</c> buffer is drawn indirectly: its step's args (the quad's
/// vertices, where they start) go up from the CPU and the copies are copied in from that
/// buffer on the GPU, so the count the passes wrote this frame is the count drawn.
/// </para>
/// </remarks>
internal sealed class StackPasses(GraphicsDevice device, CanvasFrame frame, FrameTargets targets, CanvasPipelines pipelines,
    MaterialStore materials, GlobalBindings globals) : IDisposable
{
    private readonly GraphPasses _graphs = new(device, targets, materials, globals);
    private readonly GraphicsDevice _device = device;
    private readonly CanvasFrame _frame = frame;
    private readonly List<Step> _steps = frame.Steps;
    private readonly CanvasPipelines _pipelines = pipelines;
    private readonly MaterialStore _materials = materials;
    private readonly GlobalBindings _globals = globals;


    /// <summary>
    /// Every stack drawn this frame runs the compute passes of its render graph
    /// (<see cref="StackGraph"/>) in the graph's order, before any pass draws: UPDATE, STEP,
    /// then BUILD as its <c>build()</c> calls the passes. Its TRACE and triangles run at its draw.
    /// </summary>
    public void Run(CommandBuffer commands, float dt, float time)
    {
        _graphs.BeginFrame();
        foreach (Step step in _steps)
        {
            if (step.Material is not { } material || !_materials.FirstRun(material, _frame.Number))
                continue;
            CanvasEffect effect = _pipelines.Effect(material);
            bool built = false;
            foreach (GraphPass pass in effect.Graph.Passes)
            {
                switch (pass.Brick)
                {
                    case BrickKind.Update:
                        UpdateState(commands, material, effect, dt, time);
                        break;
                    case BrickKind.Step:
                        StepPool(commands, material, effect, dt, time);
                        break;
                    // build() calls the passes, once or many times each: it runs when the graph reaches the first.
                    case BrickKind.Build when !built:
                        built = true;
                        _graphs.Run(commands, material, effect, step, dt, time);
                        break;
                }
            }

            // The graph was drawn at the defaults; this material's values may call passes they don't. BUILD is the last compute.
            if (!built)
                _graphs.Run(commands, material, effect, step, dt, time);
        }
    }

    /// <summary>A stack's UPDATE pass: its state, made zeroed on its first run, updated once.</summary>
    private void UpdateState(CommandBuffer commands, CanvasMaterial material, CanvasEffect effect, float dt, float time)
    {
        // Code that asked for state, or a lab-only effect file (Docs/shaders.md §14).
        if (!(material.KeepsState || (material.Source is null && SlangShaders.IsLabOnly(material.Shader))))
            return;
        if (effect.Update is not { } update || effect.Compiled.Update is not { } compiled)
            return;

        Entity entity = _materials.For(material);
        if (!entity.TryGetComponent(out StateBuffer held))
        {
            Buffer made = Buffer.Create<byte>(_device, "shader state",
                BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead,
                SlangShaders.MaxStateBytes);
            CopyPass copy = commands.BeginCopyPass();
            copy.UploadToBuffer(_globals.Zeros, made, cycle: false);
            commands.EndCopyPass(copy);
            entity.AddComponent(held = new StateBuffer(made));
        }

        Buffer state = held.Buffer;

        commands.PushComputeUniformData(new UpdateUniforms { Dt = dt, Time = time, Beat = ShaderClock.Beat });
        if (compiled.ParamsSize > 0)
            ShaderParams.Push(commands, compiled, material, null, stage: ParamsStage.Compute);
        ComputePass pass = commands.BeginComputePass(new StorageBufferReadWriteBinding(state, cycle: false));
        pass.BindComputePipeline(update);
        if (compiled.StorageBuffers > 0)
            pass.BindStorageBuffers(_globals.Bound(compiled.StorageBuffers));
        pass.Dispatch(1, 1, 1);
        commands.EndComputePass(pass);
    }

    /// <summary>A stack's STEP pass: its pool, made on its first run after a compile, stepped by the frame's time.</summary>
    private void StepPool(CommandBuffer commands, CanvasMaterial material, CanvasEffect effect, float dt, float time)
    {
        if (effect.Pool is not { } operation || effect.Compiled.Pool is not { } compiled)
            return;

        Entity entity = _materials.For(material);
        bool fresh = !entity.TryGetComponent(out ItemPool kept) || kept.Effect != effect;
        if (fresh)
        {
            kept.Items?.Dispose();
            // Born at a rate: two slots more, the births' counters.
            int slots = compiled.ItemCount + (compiled.PoolRate > 0 ? 2 : 0);
            entity.AddComponent(new ItemPool(effect, Buffer.Create<byte>(_device, "shader pool",
                BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead,
                (uint)(compiled.ItemBytes * slots))));
        }

        ref ItemPool pool = ref entity.GetComponent<ItemPool>();
        var job = new PoolJob { Reset = fresh ? 1u : 0u, Born = pool.Born, Parity = pool.Parity };
        if (compiled.PoolRate > 0 && !fresh)
        {
            // More births than slots can't all find one; the rest are dropped, as a full pool drops them.
            pool.Owed += compiled.PoolRate * dt;
            uint births = (uint)Math.Min(Math.Floor(pool.Owed), compiled.ItemCount);
            pool.Owed = Math.Min(pool.Owed - births, 1);
            job.Births = births;
            pool.Born += births;
            pool.Parity ^= 1;
        }

        if (compiled.ParamsSize > 0)
            ShaderParams.Push(commands, compiled, material, null, stage: ParamsStage.Compute);
        // Its inputs after the jobs: the globals, then kept state when the stack keeps some.
        operation.Submit(job);
        operation.Run(commands, pool.Items, _globals.Bound(compiled.StorageBuffers - 1, _materials.State(material)), new UpdateUniforms { Dt = dt, Time = time, Beat = ShaderClock.Beat });
    }

    /// <summary>This frame's indirect draws: a step's place in the steps, to its args' place in <see cref="_drawArgs"/>.</summary>
    private readonly Dictionary<int, uint> _drawSlots = [];
    private readonly GpuArray<uint> _drawArgs = new(device, "stack draws", BufferUsageFlags.Indirect, floor: 64 * 4);
    private readonly List<uint> _drawArgList = [];

    /// <summary>Bytes of one draw's args: vertices, instances, first vertex, first instance.</summary>
    public const uint DrawArgsBytes = 16;

    /// <summary>
    /// The most vertices one copy of a <c>[Draws]</c> stack should take before the console says so:
    /// the copies are counted on the GPU, so the vertices drawn are this times a count the CPU never
    /// sees. A quad is 6; a grid or a fine mesh under a stack of copies is thousands (XS009).
    /// </summary>
    public const uint MostCopyVertices = 96;

    private readonly HashSet<CanvasEffect> _heavyCopies = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Every step of a <c>[Draws]</c> stack gets args of its own: its vertices and where they
    /// start from the CPU, its copies from the stack's buffer, copied after the passes wrote it.
    /// </summary>
    public void FillDraws(CommandBuffer commands)
    {
        _drawSlots.Clear();
        for (int s = 0; s < _steps.Count; s++)
            if (Drawn(_steps[s]) is not null)
                _drawSlots[s] = (uint)_drawSlots.Count;
        if (_drawSlots.Count == 0)
            return;

        _drawArgList.Clear();
        foreach ((int s, uint slot) in _drawSlots)
        {
            Step step = _steps[s];
            if (step.Count > MostCopyVertices && _heavyCopies.Add(_pipelines.Effect(step.Material)))
                Console.Error.WriteLine($"Shader stack {step.Material!.Shader}: each copy it draws is {step.Count} vertices " +
                    $"(over {MostCopyVertices}), times every copy; draw it on one plain quad, with no [Grid] (XS009).");
            while (_drawArgList.Count < (slot + 1) * 4)
                _drawArgList.Add(0);
            _drawArgList[(int)slot * 4] = step.Count;
            _drawArgList[(int)slot * 4 + 2] = step.Start;
        }

        CopyPass copy = commands.BeginCopyPass();
        _drawArgs.Upload(copy, CollectionsMarshal.AsSpan(_drawArgList));
        Buffer args = _drawArgs.Buffer!;
        foreach ((int s, uint slot) in _drawSlots)
            copy.CopyBufferToBuffer(new BufferLocation { Buffer = Drawn(_steps[s])!.Handle },
                new BufferLocation { Buffer = args.Handle, Offset = slot * DrawArgsBytes + 4 }, 4, cycle: false);
        commands.EndCopyPass(copy);
    }

    /// <summary>The <c>[Draws]</c> buffer a step's stack draws by, or null when it draws a fixed count.</summary>
    private Buffer? Drawn(Step step)
    {
        if (step.Kind != StepKind.Effect || step.Material is not { } material)
            return null;
        CanvasEffect effect = _pipelines.Effect(material);
        return effect.Compiled.Build is { Shape.Draws: >= 0 and var draws } && _materials.Built(material) is { } built ? built[draws] : null;
    }

    public void Dispose()
    {
        _graphs.Dispose();
        _drawArgs.Dispose();
    }

    /// <summary>The images a material's graph presented this frame; false when it presented none.</summary>
    public bool Presented(CanvasMaterial? material, out Texture colour, out Texture? depth) => _graphs.Presented(material, out colour, out depth);

    /// <summary>The slot of a step's indirect args this frame, when its stack draws by a <c>[Draws]</c> buffer.</summary>
    public bool DrawSlot(int step, out uint slot) => _drawSlots.TryGetValue(step, out slot);

    /// <summary>The frame's indirect draw args, <see cref="DrawArgsBytes"/> a slot.</summary>
    public Buffer DrawArgs => _drawArgs.Buffer!;
}
