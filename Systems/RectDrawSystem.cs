using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

namespace Writer.Systems;

/// <summary>
/// Clears and redraws every rectangle into its entity's canvas item.
/// Full redraw every frame is intentional for now.
/// </summary>
public sealed class RectDrawSystem : QuerySystem<Rectangle, Drawable, VisualRid>
{
    private static readonly Color RectColor = Colors.White;

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref rect, ref _, ref visual, _) =>
        {
            Rect2 bounds = rect.Bounds;
            if (bounds.Size.X <= 0 || bounds.Size.Y <= 0)
                return;

            RenderingServer.CanvasItemClear(visual.Value);
            RenderingServer.CanvasItemAddRect(visual.Value, bounds, RectColor);
        });
    }
}
