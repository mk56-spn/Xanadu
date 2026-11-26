using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Systems.Rendering;

/// <summary>
/// Draws every grip with a look (<see cref="GripDot"/>, <see cref="GripSquare"/>) each frame:
/// muted blue at rest, bright blue while held (bright blue means on), a bright ring while the
/// pointer could take it. It runs after the rectangles, whose redraw would clear its canvas item.
/// </summary>
public sealed class GripDrawSystem : QuerySystem<UiGrip, VisualItem>
{
    private readonly IRectCanvas _canvas;

    public GripDrawSystem(IRectCanvas canvas)
    {
        _canvas = canvas;
        Filter.AnyTags(Tags.Get<GripDot, GripSquare>());
    }

    protected override void OnUpdate() => Query.EachEntity(new DrawGrips(_canvas, Query.Store));

    private readonly record struct DrawGrips(IRectCanvas Canvas, EntityStore Store) : IEachEntity<UiGrip, VisualItem>
    {
        public void Execute(ref UiGrip grip, ref VisualItem visual, int id)
        {
            Entity entity = Store.GetEntityById(id);
            Canvas.Clear(visual.Value);
            bool held = entity.Tags.Has<Pressed>(), lit = held || entity.Tags.Has<Hovered>();
            Color fill = held ? Palette.Primary : Palette.PrimaryMuted;
            Color edge = lit ? Palette.TextBright : Palette.PrimarySoft;
            float half = UiNode.GripSize / 2;
            if (entity.Tags.Has<GripSquare>())
            {
                var box = new Rect2(-half, -half, UiNode.GripSize, UiNode.GripSize);
                Canvas.DrawRect(visual.Value, box, fill);
                Canvas.DrawRectOutlines(visual.Value, [box], edge);
            }
            else
            {
                Canvas.DrawCircle(visual.Value, Vector2.Zero, half, fill);
                Canvas.DrawCircleOutline(visual.Value, Vector2.Zero, half, edge, lit ? 2 : 1.5f);
            }
        }
    }
}
