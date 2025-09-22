using Godot;
using Stateless;

namespace XanaduProject.Scenes.Editor.Input
{
    public abstract partial class BaseInputHandler : Control
    {
        public enum InputState { Idle, Pressed, Dragging }

        public enum Trigger { LeftDown, LeftUp, RightDown, MoveThreshold }

        private Vector2 startDragPos;
        protected virtual float DragThreshold => 10f;

        private readonly StateMachine<InputState, Trigger> stateMachine;
        private readonly StateMachine<InputState, Trigger>.TriggerWithParameters<Vector2> dragTrigger;
        private readonly StateMachine<InputState, Trigger>.TriggerWithParameters<bool> leftDownTrigger;

        protected BaseInputHandler()
        {
            stateMachine = new StateMachine<InputState, Trigger>(InputState.Idle);
            dragTrigger = stateMachine.SetTriggerParameters<Vector2>(Trigger.MoveThreshold);
            leftDownTrigger = stateMachine.SetTriggerParameters<bool>(Trigger.LeftDown);

            stateMachine.Configure(InputState.Idle)
                .Permit(Trigger.LeftDown, InputState.Pressed)
                .PermitReentry(Trigger.RightDown)
                .Ignore(Trigger.LeftUp);

            stateMachine.Configure(InputState.Pressed)
                .OnEntryFrom(leftDownTrigger, OnLeftPress)
                .Permit(Trigger.LeftUp, InputState.Idle)
                .Permit(Trigger.MoveThreshold, InputState.Dragging)
                .Permit(Trigger.RightDown, InputState.Idle);

            stateMachine.Configure(InputState.Dragging)
                .Permit(Trigger.LeftUp, InputState.Idle)
                .Permit(Trigger.RightDown, InputState.Idle)
                .InternalTransition(dragTrigger, (d, _) => OnDrag(d));

            stateMachine.OnTransitioned(t =>
            {
                if (t.Trigger == Trigger.RightDown)
                {
                    OnRightClick();
                }
                OnStateChanged(stateMachine.State);
            });
        }

        protected void ProcessInput(InputEvent @event)
        {
            switch (@event)
            {
                case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                    if (button.Pressed)
                    {
                        var multiSelect = button.ShiftPressed || button.CtrlPressed;
                        stateMachine.Fire(leftDownTrigger, multiSelect);
                    }
                    else
                    {
                        stateMachine.Fire(Trigger.LeftUp);
                    }
                    break;
                case InputEventMouseButton button:
                {
                    if (button is { ButtonIndex: MouseButton.Right, Pressed: true })
                    {
                        stateMachine.Fire(Trigger.RightDown);
                    }

                    break;
                }
                case InputEventMouseMotion motion:
                {
                    if (stateMachine.State == InputState.Pressed && startDragPos.DistanceTo(GetLocalMousePosition()) > DragThreshold)
                    {
                        stateMachine.Fire(Trigger.MoveThreshold);
                    }

                    if (stateMachine.State == InputState.Dragging)
                    {
                        var delta = motion.Relative / getCameraZoom();
                        stateMachine.Fire(dragTrigger, delta);
                    }

                    break;
                }
            }
        }

        private void OnLeftPress(bool multiSelect)
        {
            startDragPos = GetLocalMousePosition();
            HandleLeftPress(multiSelect);
        }

        private float getCameraZoom()
        {
            var camera = GetViewport().GetCamera2D();
            return camera?.Zoom.X ?? 1.0f;
        }

        // To be implemented by subclasses
        protected abstract void HandleLeftPress(bool multiSelect);
        protected abstract void OnRightClick();
        protected abstract void OnDrag(Vector2 delta);
        protected virtual void OnStateChanged(InputState state) { }
    }
}
