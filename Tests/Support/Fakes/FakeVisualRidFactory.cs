using Godot;
using Xanadu.Ecs;

namespace Xanadu.Tests.Support;

/// <summary>Visual factory that does not allocate rendering server resources.</summary>
public sealed class FakeVisualRidFactory : IVisualRidFactory
{
    public Rid CreateCanvasItem() => default;
}
