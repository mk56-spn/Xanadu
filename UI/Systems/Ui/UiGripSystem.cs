using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// Takes, drags and lets go of grips (<see cref="UiGrip"/>): marks the one the pointer could
/// take <see cref="Hovered"/>, takes it on a press, and tells it each move until the button is
/// up, wherever the pointer goes. A press a control took (<see cref="TakesInput"/>) takes no grip. The signals go out after the walk over the grips, so their
/// owners may build or hide grips as they hear them.
/// </summary>
public sealed class UiGripSystem : QuerySystem<UiGrip>
{
    private readonly IPointerInputSource _pointer;
    private Entity _held, _hovered;
    private Vector2 _last;
    private bool _wasPressed;

    public UiGripSystem(IPointerInputSource pointer)
    {
        _pointer = pointer;
        // A held grip its owner hid that frame still has to be let go.
        Filter.WithDisabled();
    }

    protected override void OnUpdate()
    {
        PointerInput input = _pointer.Read();
        bool pressed = input.Pressed && !_wasPressed;
        _wasPressed = input.Pressed;

        if (!_held.IsNull)
        {
            if (!input.Pressed)
            {
                Entity held = _held;
                _held = default;
                if (!held.IsNull)
                {
                    held.RemoveTag<Pressed>();
                    held.EmitSignal(new GripLetGo());
                }
            }
            else if (input.Position != _last)
            {
                _last = input.Position;
                _held.EmitSignal(new GripPulled(input.Position));
            }

            return;
        }

        Entity near = input.Pressed && !pressed ? default : Query.EachEntity(new Nearest(Query.Store, input.Position)).Found;
        // A press on a control over the grips (a button on the same canvas) is the control's.
        if (pressed && ControlPressed())
            near = default;
        Hover(near);
        if (pressed && !near.IsNull)
        {
            _held = near;
            _last = input.Position;
            near.AddTag<Pressed>();
            near.EmitSignal(new GripTaken(input.Position));
        }
    }

    /// <summary>Whether the input system gave this frame's press to a control (it runs first in the group).</summary>
    private bool ControlPressed() => Query.Store.Query().AllTags(Tags.Get<Pressed, TakesInput>()).Count > 0;

    private void Hover(Entity near)
    {
        if (near == _hovered)
            return;
        if (!_hovered.IsNull)
            _hovered.RemoveTag<Hovered>();
        _hovered = near;
        if (!near.IsNull)
            near.AddTag<Hovered>();
    }

    /// <summary>The shown grip in reach of the pointer with the least reach.</summary>
    private struct Nearest(EntityStore store, Vector2 pointer) : IEachEntity<UiGrip>
    {
        public Entity Found;
        private float _reach = float.MaxValue;

        public void Execute(ref UiGrip grip, int id)
        {
            Entity entity = store.GetEntityById(id);
            if (!entity.Enabled || grip.Reach >= _reach)
                return;
            if (Vector2.DistanceSquared(UiLayout.Box(entity).GetCenter(), pointer) > grip.Reach * grip.Reach)
                return;
            _reach = grip.Reach;
            Found = entity;
        }
    }
}
