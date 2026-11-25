using Friflo.Engine.ECS;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Xanadu.Host.Rendering;

// The renderer's material store (MaterialStore): one entity per material drawn, holding the GPU
// state its stack keeps between frames. Each part is there only once the stack has needed it.

/// <summary>The material an entity stands for; looked up by it.</summary>
internal struct MaterialOf(CanvasMaterial material) : IIndexedComponent<CanvasMaterial>
{
    public CanvasMaterial Material = material;

    public readonly CanvasMaterial GetIndexedValue() => Material;
}

/// <summary>A kept-state stack's state buffer, zeroed when made, written by its UPDATE pass.</summary>
internal struct StateBuffer(Buffer buffer) : IComponent
{
    public Buffer Buffer = buffer;
}

/// <summary>A pooled stack's items, made for one compile of its effect, and the births its STEP pass owes.</summary>
internal struct ItemPool(CanvasEffect effect, Buffer items) : IComponent
{
    public CanvasEffect Effect = effect;
    public Buffer Items = items;
    public double Owed;
    public uint Born;
    public uint Parity;
}

/// <summary>A BUILD stack's buffers, made for one compile of its effect.</summary>
internal struct BuildBuffers(CanvasEffect effect, Buffer[] buffers) : IComponent
{
    public CanvasEffect Effect = effect;
    public Buffer[] Buffers = buffers;
}

/// <summary>The frame a stack's compute passes last ran, so they run once a frame however many draws share it.</summary>
internal struct RanFrame(long frame) : IComponent
{
    public long Frame = frame;
}
