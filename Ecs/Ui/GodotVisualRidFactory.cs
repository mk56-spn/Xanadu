using Godot;

namespace Writer.Ecs;

/// <summary>Creates real RenderingServer canvas items.</summary>
public sealed class GodotVisualRidFactory : IVisualRidFactory
{
    public Rid CreateCanvasItem() => RenderingServer.CanvasItemCreate();
}
