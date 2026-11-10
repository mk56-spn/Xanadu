using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Drags a colour from one swatch to another: press a <see cref="UiColorSwatch"/>,
/// move past a few pixels, and let go over a <see cref="UiColorDrop"/> to hand it
/// that swatch's colour. Letting go anywhere else, or back on the swatch it came
/// from, drops nothing. While a colour is carried a swatch of it rides beside
/// the pointer, on the tooltip layer so nothing covers it.
/// </summary>
public sealed class UiColorDragSystem : BaseSystem
{
    /// <summary>How far the pointer must move before a press on a swatch becomes a drag.</summary>
    public const float DragThreshold = 6f;

    private const float GhostSide = 22f;
    private const float GhostRim = 2f;
    private static readonly Vector2 GhostOffset = new(18f, 18f);

    private readonly EntityStore entities;
    private readonly IPointerInputSource pointer;

    /// <summary>The carried colour beside the pointer, and the light rim that keeps a dark one visible.</summary>
    private readonly Entity _ghost;
    private readonly Entity _rim;

    public UiColorDragSystem(EntityStore entities, IPointerInputSource pointer, IVisualRidFactory visuals)
    {
        this.entities = entities;
        this.pointer = pointer;

        UiBuilder ui = UiBuilder.Begin(entities, visuals).Layer(UiLayers.Tooltip);
        float rim = GhostSide + GhostRim * 2;
        _rim = ui.RectEntity(Vector2.Zero, new Vector2(rim, rim), UiTheme.Text, takesInput: false);
        _ghost = ui.RectEntity(Vector2.Zero, new Vector2(GhostSide, GhostSide), Colors.White, takesInput: false);
        UiLayout.SetHidden(_rim, true);
        UiLayout.SetHidden(_ghost, true);
    }

    /// <summary>The swatch shown beside the pointer during a drag, exposed for tests.</summary>
    public Entity Ghost => _ghost;

    private Entity _source;
    private Vector2 _start;
    private bool _dragging;
    private bool _wasPressed;

    /// <summary>Whether a colour is being carried, so other tools can stay out of the way.</summary>
    public bool Dragging => _dragging;

    protected override void OnUpdateGroup()
    {
        PointerInput input = pointer.Read();
        bool started = input.Pressed && !_wasPressed;
        bool released = !input.Pressed && _wasPressed;
        _wasPressed = input.Pressed;

        if (started)
        {
            _source = Under(input.Position, swatches: true);
            _start = input.Position;
            _dragging = false;
            return;
        }

        if (_source.IsNull)
            return;

        if (input.Pressed)
        {
            if (!_dragging && input.Position.DistanceTo(_start) >= DragThreshold)
                _dragging = true;
            if (_dragging && _source.TryGetComponent(out FillColor carried))
                ShowGhost(carried.Value, input.Position);
            return;
        }

        if (released && _dragging && _source.TryGetComponent(out FillColor fill))
        {
            Entity target = Under(input.Position, swatches: false);
            if (!target.IsNull && target != _source)
            {
                ref UiColorDrop drop = ref target.GetComponent<UiColorDrop>();
                drop.Pending = true;
                drop.Color = fill.Value;
            }
        }

        _source = default;
        _dragging = false;
        HideGhost();
    }

    private void ShowGhost(Color color, Vector2 at)
    {
        Vector2 center = at + GhostOffset + new Vector2(GhostSide, GhostSide) / 2;
        Place(_rim, center);
        Place(_ghost, center);

        ref FillColor fill = ref _ghost.GetComponent<FillColor>();
        if (fill.Value != color)
        {
            fill.Value = color;
            _ghost.AddTag<Dirty>();
        }

        UiLayout.SetHidden(_rim, false);
        UiLayout.SetHidden(_ghost, false);
    }

    private static void Place(Entity entity, Vector2 center) => UiLayout.Move(entity, center);

    private void HideGhost()
    {
        UiLayout.SetHidden(_rim, true);
        UiLayout.SetHidden(_ghost, true);
    }

    /// <summary>The topmost visible swatch, or drop target, under a screen point.</summary>
    private Entity Under(Vector2 point, bool swatches)
    {
        Entity found = default;
        EntityList candidates = swatches
            ? entities.Query<FillColor>().AllTags(Tags.Get<UiColorSwatch>()).ToEntityList()
            : entities.Query<UiColorDrop>().ToEntityList();
        foreach (Entity entity in candidates)
        {
            if (!entity.TryGetComponent(out Rectangle rect) || !entity.TryGetComponent(out Position position) ||
                !Shown(entity) || InputSystem.IsClippedOut(entity, point))
            {
                continue;
            }

            Vector2 world = UiTransform.WorldPosition(entity, position.Value);
            if (new Rect2(world + rect.Bounds.Position, rect.Bounds.Size).HasPoint(point))
                found = entity;
        }

        return found;
    }

    private static bool Shown(Entity entity)
    {
        for (Entity at = entity; !at.IsNull; at = at.Parent)
        {
            if (!at.Enabled || at.Tags.Has<UiHidden>())
                return false;
        }

        return true;
    }
}
