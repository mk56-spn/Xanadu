using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Systems.Canvas;
using Writer.Systems.Input;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Debug;

/// <summary>F3 debug overlay that outlines the currently pressed input entity.</summary>
public sealed class DebugPressedOverlaySystem : QuerySystem<Rectangle, Position>
{
    private const float BorderWidth = 2;

    private static readonly Color Fill = new(1f, 0.85f, 0.2f, 0.3f);
    private static readonly Color Border = Colors.Yellow;

    private readonly IRectCanvas _canvas;
    private readonly Rid _overlay;
    private readonly IOverlayToggleSource _toggle;

    private bool _visible;
    private bool _wasTogglePressed;

    public DebugPressedOverlaySystem(
        IRectCanvas canvas,
        Rid overlay,
        IOverlayToggleSource toggle)
    {
        _canvas = canvas;
        _overlay = overlay;
        _toggle = toggle;
        Filter.AllTags(Tags.Get<Pressed>());
    }

    protected override void OnUpdate()
    {
        bool togglePressed = _toggle.IsTogglePressed();
        if (togglePressed && !_wasTogglePressed)
        {
            _visible = !_visible;
            _canvas.SetVisible(_overlay, _visible);
        }

        _wasTogglePressed = togglePressed;
        _canvas.Clear(_overlay);

        if (!_visible)
            return;

        Query.ForEachEntity((ref rect, ref position, entity) =>
        {
            Rect2 local = rect.Bounds;
            if (local.Size.X <= 0 || local.Size.Y <= 0)
                return;

            Vector2 world = UiTransform.WorldPosition(entity, position.Value);
            Rect2 bounds = new(world + local.Position, local.Size);
            DrawHighlight(bounds);
        });
    }

    private void DrawHighlight(Rect2 bounds)
    {
        _canvas.DrawRect(_overlay, bounds, Fill);

        _canvas.DrawRect(_overlay,
            new Rect2(bounds.Position, new Vector2(bounds.Size.X, BorderWidth)), Border);
        _canvas.DrawRect(_overlay,
            new Rect2(new Vector2(bounds.Position.X, bounds.End.Y - BorderWidth),
                new Vector2(bounds.Size.X, BorderWidth)), Border);
        _canvas.DrawRect(_overlay,
            new Rect2(bounds.Position, new Vector2(BorderWidth, bounds.Size.Y)), Border);
        _canvas.DrawRect(_overlay,
            new Rect2(new Vector2(bounds.End.X - BorderWidth, bounds.Position.Y),
                new Vector2(BorderWidth, bounds.Size.Y)), Border);
    }
}
