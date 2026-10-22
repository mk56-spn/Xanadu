using Godot;
using Writer.Ecs;

namespace Writer.Tests.Support;

/// <summary>Visual factory that does not allocate rendering server resources.</summary>
public sealed class FakeVisualRidFactory : IVisualRidFactory
{
    public Rid CreateCanvasItem() => default;
}
