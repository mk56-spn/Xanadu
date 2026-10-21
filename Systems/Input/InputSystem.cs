using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Input;

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
                              hoverCandidate.HasComponent<ButtonVisual>()
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
            ReleasePressed(buffer);

            if (target is { } pressed)
            {
                buffer.AddTag<Pressed>(pressed.Id);
                _pressed = pressed;
            }
        }
        else if (!input.Pressed && _wasPressed)
        {
            ReleasePressed(buffer);
        }

        _wasPressed = input.Pressed;
    }

    private void ReleasePressed(CommandBuffer buffer)
    {
        if (_pressed is not { } pressed || pressed.IsNull)
        {
            _pressed = null;
            return;
        }

        buffer.RemoveTag<Pressed>(pressed.Id);
        _pressed = null;
    }

    private readonly record struct Candidate(Entity Entity, Rect2 Bounds, int Layer);
}
