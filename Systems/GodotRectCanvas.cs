using Godot;

namespace Writer.Systems;

/// <summary>Production canvas operations backed by Godot's RenderingServer.</summary>
public sealed class GodotRectCanvas : IRectCanvas
{
    public void Clear(Rid canvas) => RenderingServer.CanvasItemClear(canvas);

    public void DrawRect(Rid canvas, Rect2 bounds, Color color) =>
        RenderingServer.CanvasItemAddRect(canvas, bounds, color);
}
