using Godot;

namespace Writer.Ui;

/// <summary>Creates real RenderingServer canvas items.</summary>
public sealed class GodotVisualRidFactory : IVisualRidFactory
{
    public Rid CreateCanvasItem() => RenderingServer.CanvasItemCreate();
}
