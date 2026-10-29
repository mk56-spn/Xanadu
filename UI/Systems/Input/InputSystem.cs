using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Input;

/// <summary>
/// Hit-tests <see cref="TakesInput"/> drawables in descending layer order and
/// tags the topmost hit with <see cref="Pressed"/> while the pointer is down.
/// </summary>
public sealed class InputSystem : QuerySystem<Rectangle, Drawable, Position>
{
    private readonly IPointerInputSource _source;
    private readonly List<Candidate> _candidates = new();

    private Entity? _pressed;
    private Entity? _hovered;
    private bool _wasPressed;

    public InputSystem(IPointerInputSource source)
    {
        _source = source;
        Filter.AllTags(Tags.Get<TakesInput>());
    }

    protected override void OnUpdate()
    {
        PointerInput input = _source.Read();
        _candidates.Clear();

        Query.ForEachEntity((ref rect, ref drawable, ref position, entity) =>
        {
            Rect2 local = rect.Bounds;
            Vector2 world = UiTransform.WorldPosition(entity, position.Value);
            Rect2 screen = new(world + local.Position, local.Size);
            if (!IsClippedOut(entity, input.Position))
                _candidates.Add(new Candidate(entity, screen, drawable.ZIndex));
        });

        _candidates.Sort(static (left, right) =>
        {
            int layer = right.Layer.CompareTo(left.Layer);
            return layer != 0 ? layer : right.Entity.Id.CompareTo(left.Entity.Id);
        });

        Entity? target = null;
        foreach (Candidate candidate in _candidates)
        {
            if (!candidate.Bounds.HasPoint(input.Position))
                continue;

            target = candidate.Entity;
            break;
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
    }

    /// <summary>True when a clipping ancestor hides the point, e.g. scrolled-out rows.</summary>
    private static bool IsClippedOut(Entity entity, Vector2 point)
    {
        for (Entity parent = entity.Parent; !parent.IsNull; parent = parent.Parent)
        {
            if (!parent.TryGetComponent(out UiContainer container) || !container.Clip ||
                !parent.TryGetComponent(out Rectangle rect) ||
                !parent.TryGetComponent(out Position position))
            {
                continue;
            }

            Vector2 world = UiTransform.WorldPosition(parent, position.Value);
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

    private readonly record struct Candidate(Entity Entity, Rect2 Bounds, int Layer);
}
