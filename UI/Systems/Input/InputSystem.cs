using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Systems.Input;

/// <summary>
/// Hit-tests <see cref="TakesInput"/> drawables in paint order and tags the
/// topmost hit with <see cref="Pressed"/> while the pointer is down, and with
/// <see cref="SecondaryPressed"/> while the right button is.
/// </summary>
public sealed class InputSystem : QuerySystem<Rectangle, Drawable, Position>
{
    private readonly IPointerInputSource _source;
    private readonly List<Candidate> _candidates = [];

    private Entity? _pressed;
    private Entity? _secondary;
    private Entity? _hovered;
    private bool _wasPressed;
    private bool _wasSecondary;

    public InputSystem(IPointerInputSource source)
    {
        _source = source;
        Filter.AllTags(Tags.Get<TakesInput>());
        Filter.WithoutAnyTags(Tags.Get<UiDisabled>());
    }

    protected override void OnUpdate()
    {
        PointerInput input = _source.Read();
        _candidates.Clear();

        Query.EachEntity(new UnderPointer(Query.Store, _candidates, input.Position));

        Entity? target = null;
        foreach (Candidate candidate in _candidates)
        {
            if (target is not { } best || IsAbove(candidate, best))
                target = candidate.Entity;
        }

        CommandBuffer buffer = CommandBuffer;

        Entity? hoverTarget = target is { } hoverCandidate &&
                              (hoverCandidate.HasComponent<ButtonVisual>() ||
                               hoverCandidate.HasComponent<UiTooltip>())
            ? hoverCandidate
            : null;

        if (_hovered is { } previous &&
            (!hoverTarget.HasValue ||
             previous.Id != hoverTarget.Value.Id))
        {
            if (!previous.IsNull)
                buffer.RemoveTag<Hovered>(previous.Id);

            _hovered = null;
        }

        if (hoverTarget is { } hover &&
            (!_hovered.HasValue || _hovered.Value.Id != hover.Id))
        {
            buffer.AddTag<Hovered>(hover.Id);
            _hovered = hover;
        }

        if (input.Pressed && !_wasPressed)
        {
            ReleasePressed();

            if (target is { } pressed)
            {
                // Add the tag immediately, not through the command buffer: the
                // dropdown and scene systems later in this frame must see the
                // same press, and the buffer is not flushed until the group ends.
                pressed.AddTag<Pressed>();
                _pressed = pressed;
            }
        }
        else if (!input.Pressed && _wasPressed)
        {
            ReleasePressed();
        }

        _wasPressed = input.Pressed;

        // The right button marks what it went down on the same way, for menus.
        if (input.SecondaryPressed && !_wasSecondary)
        {
            Release(ref _secondary);
            if (target is { } secondary)
            {
                secondary.AddTag<SecondaryPressed>();
                _secondary = secondary;
            }
        }
        else if (!input.SecondaryPressed && _wasSecondary)
            Release(ref _secondary);

        _wasSecondary = input.SecondaryPressed;
    }

    /// <summary>
    /// Whether this frame's press went to something other than <paramref name="entity"/>, what holds it or what it
    /// holds: a control over it (a menu's entry), so a reader of the raw pointer such as a text editor leaves it alone.
    /// Called on a press only: the press can land on a control that hides itself that frame, so hidden ones count.
    /// </summary>
    public static bool PressedElsewhere(Entity entity)
    {
        foreach (Entity pressed in entity.Store.Query().AllTags(Tags.Get<Pressed>()).WithDisabled().ToEntityList())
        {
            if (!Holds(pressed, entity) && !Holds(entity, pressed))
                return true;
        }

        return false;
    }

    private static bool Holds(Entity outer, Entity inner)
    {
        for (Entity at = inner; !at.IsNull; at = at.Parent)
        {
            if (at.Id == outer.Id)
                return true;
        }

        return false;
    }

    private static void Release(ref Entity? held)
    {
        if (held is { IsNull: false } entity)
            entity.RemoveTag<SecondaryPressed>();
        held = null;
    }

    /// <summary>True when a clipping ancestor hides the point, e.g. scrolled-out rows.</summary>
    public static bool IsClippedOut(Entity entity, Vector2 point)
    {
        for (Entity parent = entity.Parent; !parent.IsNull; parent = parent.Parent)
        {
            if (!parent.TryGetComponent(out UiContainer container) || !container.Clip ||
                !parent.TryGetComponent(out Rectangle rect) ||
                !parent.TryGetComponent(out Position position))
            {
                continue;
            }

            Vector2 world = UiTransform.ShownPosition(parent, position.Value);
            if (!new Rect2(world + rect.Bounds.Position, rect.Bounds.Size).HasPoint(point))
                return true;
        }

        return false;
    }

    private void ReleasePressed()
    {
        if (_pressed is not { } pressed || pressed.IsNull)
        {
            _pressed = null;
            return;
        }

        pressed.RemoveTag<Pressed>();
        _pressed = null;
    }

    /// <summary>
    /// Whether a hit paints over the best so far: the higher layer, then by the
    /// tree, where a child is above its parent and a later sibling above an earlier
    /// one. Ids say nothing about this, because deleted ids are recycled.
    /// </summary>
    private bool IsAbove(in Candidate candidate, Entity best)
    {
        int layer = candidate.Layer.CompareTo(best.GetComponent<Drawable>().ZIndex);
        return layer != 0 ? layer > 0 : UiTreeOrder.IsPaintedAfter(candidate.Entity, best);
    }

    private readonly record struct Candidate(Entity Entity, int Layer);

    private readonly record struct UnderPointer(EntityStore Store, List<Candidate> Candidates, Vector2 Pointer) : IEachEntity<Rectangle, Drawable, Position>
    {
        public void Execute(ref Rectangle rect, ref Drawable drawable, ref Position position, int id)
        {
            Entity entity = Store.GetEntityById(id);

            Rect2 local = rect.Bounds;
            Vector2 world = UiTransform.ShownPosition(entity, position.Value);
            Rect2 screen = new(world + local.Position, local.Size);
            if (screen.HasPoint(Pointer) && !IsClippedOut(entity, Pointer))
                Candidates.Add(new Candidate(entity, drawable.ZIndex));
        }
    }
}
