using MoonWorks.Graphics;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Xanadu.Rendering.Passes;

/// <summary>
/// The canvas as a stack the renderer runs itself (<c>Assets/Shaders/Engine/canvas</c>, Docs/render-graph.md "The
/// resident UI on the graph"): the primitive pool, the CPU's instances and the resident trees are its host lists, and
/// its graph patches the trees, resolves their slots and expands every instance's primitives into <see cref="Placed"/>
/// and <see cref="Opaque"/> for the draws.
/// </summary>
internal sealed class CanvasStack(GraphRunner graphs, CanvasPipelines pipelines)
{
    public const string Path = "Assets/Shaders/Engine/canvas";

    private Buffer? _placed, _opaque;

    /// <summary>The material the renderer runs the stack with: what its passes get from the host.</summary>
    public CanvasMaterial Material { get; } = new(Path, instance: "canvas");

    /// <summary>The frame's primitives in pixels and draw order, as the draws read them.</summary>
    public Buffer Placed => _placed ?? throw new InvalidOperationException("the canvas stack hasn't run yet");

    /// <summary>The pre-pass list: a slot per placed primitive, its opaque cut or a skip.</summary>
    public Buffer Opaque => _opaque ?? throw new InvalidOperationException("the canvas stack hasn't run yet");

    /// <summary>Runs the stack's graph for this frame, its host lists sent first.</summary>
    public void Run(CommandBuffer commands)
    {
        Buffer[] buffers = graphs.RunHosted(commands, Material)
            ?? throw new InvalidOperationException($"{Path} didn't compile as a graph: nothing plain can draw (see the shader log).");
        Xanadu.Slang.StackBuild shape = pipelines.Effect(Material).Compiled.Build!.Shape;
        _placed = buffers[Index(shape, "placed")];
        _opaque = buffers[Index(shape, "opaque")];
    }

    private static int Index(Xanadu.Slang.StackBuild shape, string name)
    {
        for (int k = 0; k < shape.Buffers.Count; k++)
            if (shape.Buffers[k].Name == name)
                return k;
        throw new InvalidOperationException($"{Path} declares no list {name}.");
    }
}
