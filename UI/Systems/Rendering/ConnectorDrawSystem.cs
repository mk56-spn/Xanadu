using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Rendering;

/// <summary>
/// Redraws every <see cref="UiConnector"/> each frame from its ends' laid-out
/// boxes, since either can move or hide without the connector hearing of it.
/// It runs after the rectangles, whose redraw would clear its canvas item.
/// </summary>
public sealed class ConnectorDrawSystem : QuerySystem<UiConnector, Drawable, VisualRid>
{
    private readonly IRectCanvas _canvas;

    public ConnectorDrawSystem(IRectCanvas canvas) => _canvas = canvas;

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref line, ref _, ref visual, entity) =>
        {
            _canvas.Clear(visual.Value);
            if (!Shown(line.From) || !Shown(line.To))
                return;

            Rect2 from = UiLayout.Box(line.From);
            Rect2 to = UiLayout.Box(line.To);
            Vector2 origin = UiLayout.Box(entity).GetCenter();

            // Each end leaves from the side that faces the other.
            bool rightward = from.GetCenter().X <= to.GetCenter().X;
            var start = new Vector2(rightward ? from.End.X : from.Position.X, from.GetCenter().Y);
            var end = new Vector2(rightward ? to.Position.X : to.End.X, to.GetCenter().Y);
            _canvas.DrawLine(visual.Value, start - origin, end - origin, line.Color, line.Width);
        });
    }

    private static bool Shown(Entity end) =>
        !end.IsNull && end.Enabled && end.HasComponent<Rectangle>();
}
