using Friflo.Engine.ECS;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Xanadu.Rendering.Kept;

// The renderer's material store (MaterialStore): one entity per material drawn, holding the GPU
// state its stack's graph keeps between frames. Each part is there only once the graph has needed it.

/// <summary>The material an entity stands for; looked up by it.</summary>
internal struct MaterialOf(CanvasMaterial material) : IIndexedComponent<CanvasMaterial>
{
    public CanvasMaterial Material = material;

    public readonly CanvasMaterial GetIndexedValue() => Material;
}

/// <summary>
/// A graph's buffers, the material's own, made for one compile of its effect. Its images are each item's (ItemImages).
/// <see cref="HostVersion"/> is the material's <see cref="CanvasMaterial.BufferVersion"/> its <c>[Host]</c> buffers hold.
/// </summary>
internal struct BuildBuffers(CanvasEffect effect, Buffer[] buffers) : IComponent
{
    public CanvasEffect Effect = effect;
    public Buffer[] Buffers = buffers;
    public int HostVersion = -1;

    /// <summary>The material's stamp when its <c>[Cached]</c> buffers were last filled; -1 until they are.</summary>
    public long Filled = -1;

    /// <summary>
    /// Indirect args for the graph's draws counted on the GPU, 16 bytes a pass by its place: its vertices, then the copies
    /// copied in from its count each frame (<c>GraphResources.Count</c>); null when no draw is counted.
    /// </summary>
    public Buffer? DrawArgs;
}

/// <summary>
/// A schedule's graph lowered with its material's values (<see cref="Xanadu.Slang.GraphPlan"/>), for one compile of its
/// effect: again each frame while it reads values, once when it reads none.
/// </summary>
internal struct LoweredGraph(CanvasEffect effect, Xanadu.Slang.GraphPlan plan) : IComponent
{
    public CanvasEffect Effect = effect;
    public Xanadu.Slang.GraphPlan Plan = plan;
}

/// <summary>The frame a stack's compute passes last ran, so they run once a frame however many draws share it.</summary>
internal struct RanFrame(long frame) : IComponent
{
    public long Frame = frame;
}
