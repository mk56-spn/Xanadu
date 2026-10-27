using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Generic pointer interaction for <see cref="UiWheel"/> entities: hover the
/// wedge under the cursor, click to select it, and keep selecting by angle
/// while the pointer is dragged around the wheel.
/// </summary>
public sealed class UiWheelSystem : QuerySystem<UiWheel, Drawable>
{
    private readonly EntityStore _entities;
    private readonly IPointerInputSource _pointer;

    private int _activeWheelId;

    public UiWheelSystem(
        EntityStore entities,
        IPointerInputSource pointer)
    {
        _entities = entities;
        _pointer = pointer;
    }

    protected override void OnUpdate()
    {
        PointerInput input = _pointer.Read();
        EntityList wheels = Query.ToEntityList();
        CommandBuffer buffer = CommandBuffer;

        Entity hovered = FindTopmost(wheels, input.Position);

        // InputSystem owns the click latch and the Pressed tag, so a quick
        // press/release between frames still reaches the wheel.
        if (_activeWheelId == 0)
        {
            foreach (Entity wheel in wheels)
            {
                if (!wheel.Tags.Has<Pressed>() ||
                    !UiWheelGeometry.Contains(wheel, input.Position))
                {
                    continue;
                }

                _activeWheelId = wheel.Id;
                SelectAt(wheel, input.Position, allowOutside: false, buffer);
                break;
            }
        }

        if (_activeWheelId != 0)
        {
            Entity active = _entities.GetEntityById(_activeWheelId);
            if (active.IsNull ||
                !active.HasComponent<UiWheel>() ||
                active.Tags.Has<Disabled>() ||
                !active.Tags.Has<Pressed>())
            {
                _activeWheelId = 0;
            }
            else
            {
                SelectAt(active, input.Position, allowOutside: true, buffer);
            }
        }

        foreach (Entity wheel in wheels)
        {
            if (!wheel.HasComponent<UiWheel>())
                continue;

            int hover = !hovered.IsNull && hovered.Id == wheel.Id
                ? UiWheelGeometry.OptionAt(wheel, input.Position)
                : -1;
            ref UiWheel data = ref wheel.GetComponent<UiWheel>();
            if (data.Hover == hover)
                continue;

            data.Hover = hover;
            buffer.AddTag<Dirty>(wheel.Id);
        }
    }

    private static Entity FindTopmost(EntityList wheels, Vector2 pointer)
    {
        Entity topmost = default;
        int topLayer = int.MinValue;

        foreach (Entity wheel in wheels)
        {
            if (!UiWheelGeometry.Contains(wheel, pointer))
                continue;

            int layer = wheel.GetComponent<Drawable>().ZIndex;
            if (!topmost.IsNull &&
                (layer < topLayer ||
                 (layer == topLayer && wheel.Id <= topmost.Id)))
            {
                continue;
            }

            topmost = wheel;
            topLayer = layer;
        }

        return topmost;
    }

    private static void SelectAt(
        Entity wheel,
        Vector2 pointer,
        bool allowOutside,
        CommandBuffer buffer)
    {
        int index = UiWheelGeometry.OptionAt(wheel, pointer, allowOutside);
        if (index < 0)
            return;

        ref UiWheel data = ref wheel.GetComponent<UiWheel>();
        if (data.Selected == index)
            return;

        data.Selected = index;
        buffer.AddTag<Dirty>(wheel.Id);
        buffer.AddComponent(wheel.Id, new UiWheelChanged(index));
    }
}
