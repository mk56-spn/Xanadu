using Godot;
using Stateless;

namespace XanaduProject.Screens.AssetCreation.Editor.Input
{
    public abstract partial class BaseInputHandler : Control
    {
        public enum InputState { Idle, Pressed, Dragging, RightPressed, RightDragging }

        public enum Trigger { LeftDown, LeftUp, RightDown, RightUp, MoveThreshold, RightMoveThreshold }

        private Vector2 startDragPos;
        protected virtual float DragThreshold => 10f;

        private readonly StateMachine<InputState, Trigger> stateMachine;
        private readonly StateMachine<InputState, Trigger>.TriggerWithParameters<Vector2> dragTrigger;
        private readonly StateMachine<InputState, Trigger>.TriggerWithParameters<Vector2> rightDragTrigger;
        private readonly StateMachine<InputState, Trigger>.TriggerWithParameters<bool> leftDownTrigger;

        protected BaseInputHandler()
        {
            stateMachine = new StateMachine<InputState, Trigger>(InputState.Idle);
            dragTrigger = stateMachine.SetTriggerParameters<Vector2>(Trigger.MoveThreshold);
            rightDragTrigger = stateMachine.SetTriggerParameters<Vector2>(Trigger.RightMoveThreshold);
            leftDownTrigger = stateMachine.SetTriggerParameters<bool>(Trigger.LeftDown);

            stateMachine.Configure(InputState.Idle)
                .Permit(Trigger.LeftDown, InputState.Pressed)
                .Permit(Trigger.RightDown, InputState.RightPressed)
                .Ignore(Trigger.LeftUp)
                .Ignore(Trigger.RightUp);

            stateMachine.Configure(InputState.Pressed)
                .OnEntryFrom(leftDownTrigger, OnLeftPress)
                .Permit(Trigger.LeftUp, InputState.Idle)
                .Permit(Trigger.MoveThreshold, InputState.Dragging);

            stateMachine.Configure(InputState.Dragging)
                .Permit(Trigger.LeftUp, InputState.Idle)
                .OnExit(() => OnDragEnd())
                .InternalTransition(dragTrigger, (d, _) => OnDrag(d));

            stateMachine.Configure(InputState.RightPressed)
                .Permit(Trigger.RightUp, InputState.Idle)
                .Permit(Trigger.RightMoveThreshold, InputState.RightDragging);

            stateMachine.Configure(InputState.RightDragging)
                .Permit(Trigger.RightUp, InputState.Idle)
                .OnExit(() => OnDragEnd())
                .InternalTransition(rightDragTrigger, (d, _) => OnRightDrag(d));

            stateMachine.OnTransitioned(t =>
            {
                if (t.Trigger == Trigger.RightDown)
                {
                    OnRightClick();
                }

                if (t.Trigger == Trigger.LeftUp)
                {
                    HandleLeftRelease();
                }

                if (t.Trigger == Trigger.RightUp)
                {
                    HandleRightRelease();
                }
                OnStateChanged(stateMachine.State);
            });
        }
        public override void _GuiInput(InputEvent @event)
        {
            ProcessInput(@event);
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
                    else if (button is { ButtonIndex: MouseButton.Right, Pressed: false })
                    {
                        stateMachine.Fire(Trigger.RightUp);
                    }

                    break;
                }
                case InputEventMouseMotion motion:
                {
                    if (stateMachine.State == InputState.Pressed && startDragPos.DistanceTo(GetLocalMousePosition()) > DragThreshold)
                    {
                        stateMachine.Fire(Trigger.MoveThreshold);
                    }

                    if (stateMachine.State == InputState.RightPressed && startDragPos.DistanceTo(GetLocalMousePosition()) > DragThreshold)
                    {
                        stateMachine.Fire(Trigger.RightMoveThreshold);
                    }

                    if (stateMachine.State == InputState.Dragging)
                    {
                        var delta = motion.Relative / getCameraZoom();
                        stateMachine.Fire(dragTrigger, delta);
                    }

                    if (stateMachine.State == InputState.RightDragging)
                    {
                        var delta = motion.Relative / getCameraZoom();
                        stateMachine.Fire(rightDragTrigger, delta);
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
        protected virtual void HandleLeftRelease(){}
        protected virtual void HandleRightRelease(){}
        protected abstract void OnRightClick();
        protected abstract void OnDrag(Vector2 delta);
        protected virtual void OnRightDrag(Vector2 delta) { }
        protected virtual void OnStateChanged(InputState state) { }
        protected virtual void OnDragEnd() { }
    }
}
