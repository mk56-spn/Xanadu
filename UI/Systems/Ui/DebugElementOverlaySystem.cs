using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Input;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// F4 boxes around buttons and containers; the tree panel carries their legend. The entity picked in the F4
/// tree, and the one under the cursor or behind the hovered row, are drawn last in their own colours, so
/// their children's boxes cannot hide them; one without a box gets a cross.
/// </summary>
public sealed class DebugElementOverlaySystem : QuerySystem<Rectangle, Position>
{
    private readonly IRectCanvas _canvas;
    private readonly Handle _overlay;
    private readonly OverlayLatch _latch;
    private readonly RetainedCanvasDraw _draw;

    public DebugElementOverlaySystem(IRectCanvas canvas, Handle overlay, IOverlayToggleSource toggle)
    {
        _canvas = canvas;
        _overlay = overlay;
        _latch = new OverlayLatch(toggle);
        _draw = new RetainedCanvasDraw(canvas, overlay);
        Filter.WithoutAnyTags(Tags.Get<DebugUi>());
    }

    protected override void OnUpdate()
    {
        if (_latch.Update())
        {
            _canvas.SetVisible(_overlay, _latch.Visible);
            if (!_latch.Visible)
                _draw.Reset();
        }

        if (!_latch.Visible)
            return;

        _draw.Begin();

        Query.EachEntity(new DrawElements(this, Query.Store));

        // Last, so nothing inside them can hide them: the inspected entity, then what the cursor is on.
        Mark(ref _inspected, Tags.Get<UiTreeOverlaySelected>(), DebugPalette.SelectedFill, DebugPalette.SelectedBorder);
        Mark(ref _hovered, Tags.Get<UiTreeOverlayHovered>(), DebugPalette.HoveredFill, DebugPalette.HoveredBorder);

        _draw.End();
    }

    private ArchetypeQuery<Position>? _inspected;
    private ArchetypeQuery<Position>? _hovered;

    /// <summary>The F4 tree's marked entity: its box, thick, or a cross at its position when it has none.</summary>
    private void Mark(ref ArchetypeQuery<Position>? query, Tags tag, Color fill, Color border)
    {
        query ??= Query.Store.Query<Position>().AllTags(tag);
        query.EachEntity(new DrawMark(this, Query.Store, fill, border));
    }

    /// <summary>Whether the entity is part of a debug panel, F4's own among them, which get no boxes.</summary>
    private static bool InDebugView(Entity entity)
    {
        for (Entity up = entity; !up.IsNull; up = up.Parent)
        {
            if (up.Tags.Has<DebugUi>())
                return true;
        }
        return false;
    }


    private void DrawBounds(
        Rect2 bounds,
        Color fill,
        Color border,
        float width = UiMetrics.Border.Thin)
    {
        _draw.Rect(bounds, fill);
        _draw.Rect(
            new Rect2(bounds.Position, new Vector2(bounds.Size.X, width)),
            border);
        _draw.Rect(
            new Rect2(
                new Vector2(bounds.Position.X, bounds.End.Y - width),
                new Vector2(bounds.Size.X, width)),
            border);
        _draw.Rect(
            new Rect2(bounds.Position, new Vector2(width, bounds.Size.Y)),
            border);
        _draw.Rect(
            new Rect2(
                new Vector2(bounds.End.X - width, bounds.Position.Y),
                new Vector2(width, bounds.Size.Y)),
            border);
    }

    private readonly record struct DrawElements(DebugElementOverlaySystem System, EntityStore Store) : IEachEntity<Rectangle, Position>
    {
        public void Execute(ref Rectangle rect, ref Position position, int id)
        {
            Entity entity = Store.GetEntityById(id);

            if (rect.Bounds.Size.X <= 0 || rect.Bounds.Size.Y <= 0)
                return;

            bool button = entity.HasComponent<ButtonVisual>();
            bool container = entity.HasComponent<UiContainer>();
            bool marked = entity.Tags.HasAny(Tags.Get<UiTreeOverlaySelected, UiTreeOverlayHovered>());
            if ((!button && !container) || marked || InDebugView(entity))
                return;

            Vector2 world = UiTransform.WorldPosition(entity, position.Value);
            var bounds = new Rect2(world + rect.Bounds.Position, rect.Bounds.Size);
            if (button)
                System.DrawBounds(bounds, DebugPalette.ButtonFill, DebugPalette.ButtonBorder);
            if (container)
                System.DrawBounds(bounds, DebugPalette.ContainerFill, DebugPalette.ContainerBorder);

            if (entity.TryGetComponent(out UiAnchor anchor))
            {
                Vector2 pin = world + rect.Bounds.Position + rect.Bounds.Size * anchor.Anchor;
                System._draw.Circle(pin, 6, DebugPalette.Pin);
                System._draw.CircleOutline(pin, 7, DebugPalette.PinOutline, 1);
            }
        }
    }

    private readonly record struct DrawMark(DebugElementOverlaySystem System, EntityStore Store, Color Fill, Color Border) : IEachEntity<Position>
    {
        public void Execute(ref Position position, int id)
        {
            Entity entity = Store.GetEntityById(id);

            Vector2 at = UiTransform.WorldPosition(entity, position.Value);
            if (entity.TryGetComponent(out Rectangle rect) && rect.Bounds.Size.X > 0 && rect.Bounds.Size.Y > 0)
            {
                System.DrawBounds(new Rect2(at + rect.Bounds.Position, rect.Bounds.Size), Fill, Border, UiMetrics.Border.Thick);
                return;
            }

            const float arm = 14;
            float width = UiMetrics.Border.Medium;
            System._draw.Rect(new Rect2(at - new Vector2(arm, width / 2), new Vector2(arm * 2, width)), Border);
            System._draw.Rect(new Rect2(at - new Vector2(width / 2, arm), new Vector2(width, arm * 2)), Border);
            System._draw.CircleOutline(at, arm * 0.6f, Border, 1);
        }
    }
}
