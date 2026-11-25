using System.Collections.Generic;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Rendering.Frame;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Xanadu.Rendering.Passes;

/// <summary>
/// Shade exits drawn by a <c>[Draws]</c> buffer: every step of such a stack gets args of its own, its vertices and where
/// they start from the CPU, its copies copied in on the GPU from the buffer the graph's nodes wrote this frame, so the
/// count they wrote is the count drawn.
/// </summary>
internal sealed class IndirectDraws(GraphicsDevice device, CanvasFrame frame, CanvasPipelines pipelines, MaterialStore materials) : IDisposable
{
    /// <summary>Bytes of one draw's args: vertices, instances, first vertex, first instance.</summary>
    public const uint ArgsBytes = 16;

    /// <summary>
    /// The most vertices one copy should take before the console says so: the copies are counted on the GPU, so the
    /// vertices drawn are this times a count the CPU never sees. A quad is 6; a grid or a fine mesh under a stack of
    /// copies is thousands (XS009).
    /// </summary>
    private const uint MostCopyVertices = 96;

    private readonly List<Step> _steps = frame.Steps;

    /// <summary>This frame's indirect draws: a step's place in the steps, to its args' place in <see cref="_args"/>.</summary>
    private readonly Dictionary<int, uint> _slots = [];
    private readonly GpuArray<uint> _args = new(device, "stack draws", BufferUsageFlags.Indirect, floor: 64 * 4);
    private readonly List<uint> _argList = [];
    private readonly HashSet<CanvasEffect> _heavyCopies = new(ReferenceEqualityComparer.Instance);

    /// <summary>The frame's args, <see cref="ArgsBytes"/> a slot.</summary>
    public Buffer Args => _args.Buffer!;

    /// <summary>The slot of a step's args this frame, when its stack draws by a <c>[Draws]</c> buffer.</summary>
    public bool Slot(int step, out uint slot) => _slots.TryGetValue(step, out slot);

    /// <summary>Fills the frame's args, after the graphs' nodes wrote their counts.</summary>
    public void Fill(CommandBuffer commands)
    {
        _slots.Clear();
        for (int s = 0; s < _steps.Count; s++)
            if (Drawn(_steps[s]) is not null)
                _slots[s] = (uint)_slots.Count;
        if (_slots.Count == 0)
            return;

        _argList.Clear();
        foreach ((int s, uint slot) in _slots)
        {
            Step step = _steps[s];
            if (step.Count > MostCopyVertices && _heavyCopies.Add(pipelines.Effect(step.Material)))
                Console.Error.WriteLine($"Shader stack {step.Material!.Shader}: each copy it draws is {step.Count} vertices " +
                    $"(over {MostCopyVertices}), times every copy; draw it on one plain quad, with no [Grid] (XS009).");
            while (_argList.Count < (slot + 1) * 4)
                _argList.Add(0);
            _argList[(int)slot * 4] = step.Count;
            _argList[(int)slot * 4 + 2] = step.Start;
        }

        CopyPass copy = commands.BeginCopyPass();
        _args.Upload(copy, CollectionsMarshal.AsSpan(_argList));
        foreach ((int s, uint slot) in _slots)
            copy.CopyBufferToBuffer(new BufferLocation { Buffer = Drawn(_steps[s])!.Handle },
                new BufferLocation { Buffer = Args.Handle, Offset = slot * ArgsBytes + 4 }, 4, cycle: false);
        commands.EndCopyPass(copy);
    }

    /// <summary>The <c>[Draws]</c> buffer a step's stack draws by, or null when it draws a fixed count.</summary>
    private Buffer? Drawn(Step step)
    {
        if (step.Kind != StepKind.Effect || step.Material is not { } material)
            return null;
        CanvasEffect effect = pipelines.Effect(material);
        return effect.Compiled.Build is { Shape.Draws: >= 0 and var draws } && materials.Built(material) is { } built ? built[draws] : null;
    }

    public void Dispose() => _args.Dispose();
}
