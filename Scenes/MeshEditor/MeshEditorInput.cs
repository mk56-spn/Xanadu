using System;
using Godot;
using Stateless;

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class MeshEditorInput : Control
    {
        private const float point_selection_radius = 10f;
        private const float handle_selection_radius = 8f;
        private const float drag_threshold = 5f;

        private readonly IMeshEditor editor;
        private readonly StateMachine<State, Trigger> stateMachine;
        private Vector2 pressPosition;

        private enum State { Idle, Pressed, Dragging }
        private enum Trigger { LeftDown, LeftUp, RightDown, MouseMoved }

        public MeshEditorInput(IMeshEditor editor)
        {
            this.editor = editor;

            stateMachine = new StateMachine<State, Trigger>(State.Idle);

            stateMachine.Configure(State.Idle)
                .Ignore(Trigger.LeftUp)
                .Permit(Trigger.LeftDown, State.Pressed)
                .PermitReentry(Trigger.RightDown);

            stateMachine.Configure(State.Pressed)
                .OnEntry(handlePress)
                .Permit(Trigger.LeftUp, State.Idle)
                .Permit(Trigger.MouseMoved, State.Dragging)
                .Permit(Trigger.RightDown, State.Idle);

            stateMachine.Configure(State.Dragging)
                .OnEntry(handleDragStart)
                .Permit(Trigger.LeftUp, State.Idle)
                .Permit(Trigger.RightDown, State.Idle);

            stateMachine.OnTransitioned(transition =>
            {
                if (transition.Trigger == Trigger.RightDown) handleRightClick();
            });

            Ready += () => SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }


        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButton)
            {
                if (mouseButton.ButtonIndex == MouseButton.Left)
                {
                    stateMachine.Fire(mouseButton.Pressed ? Trigger.LeftDown : Trigger.LeftUp);
                }
                else if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
                {
                    stateMachine.Fire(Trigger.RightDown);
                }
            }
            else if (@event is InputEventMouseMotion && stateMachine.State == State.Pressed)
            {
                if (pressPosition.DistanceTo(GetGlobalMousePosition()) > drag_threshold)
                {
                    stateMachine.Fire(Trigger.MouseMoved);
                }
            }
        }

        private void handlePress()
        {
            pressPosition = GetGlobalMousePosition();
            ref var activeMeshComponent = ref editor.LayerManager.ActiveMesh.GetComponent<MeshComponent>();

            (int clickedPointIndex, HandleType clickedHandleType) = findClickedBezierElement(pressPosition);

            if (clickedPointIndex != -1)
            {
                activeMeshComponent.SelectedBezierPointIndex = clickedPointIndex;
                activeMeshComponent.SelectedHandleType = clickedHandleType;
            }
            else
            {
                // Add new Bezier point
                var points = activeMeshComponent.BezierPoints;
                BezierPoint newPoint = new BezierPoint(pressPosition);
                newPoint.HandlesLocked = false; // Default to unlocked

                if (points.Count > 0)
                {
                    // Previous point is the last one in the list
                    var prevPoint = points[points.Count - 1];
                    var inHandleDir = (prevPoint.Position - newPoint.Position).Normalized();
                    float inHandleLength = (prevPoint.Position - newPoint.Position).Length() / 3f;
                    newPoint.InHandle = inHandleDir * inHandleLength;

                    // Next point is the first one in the list (closed loop)
                    var nextPoint = points[0];
                    var outHandleDir = (nextPoint.Position - newPoint.Position).Normalized();
                    float outHandleLength = (nextPoint.Position - newPoint.Position).Length() / 3f;
                    newPoint.OutHandle = outHandleDir * outHandleLength;
                }

                activeMeshComponent.BezierPoints.Add(newPoint);
                activeMeshComponent.SelectedBezierPointIndex = activeMeshComponent.BezierPoints.Count - 1;
                activeMeshComponent.SelectedHandleType = HandleType.Point;
                editor.LayerManager.UpdateTriangulationForMesh(activeMeshComponent); // Use LayerManager's triangulation with MeshComponent
            }
            editor.LayerManager.NotifyMeshSelectionChanged(); // Notify UI about selection change
        }

        private (int, HandleType) findClickedBezierElement(Vector2 position)
        {
            ref var meshData = ref editor.LayerManager.ActiveMesh.GetComponent<MeshComponent>();

            for (int i = 0; i < meshData.BezierPoints.Count; i++)
            {
                BezierPoint bp = meshData.BezierPoints[i];

                // Check main point
                if (bp.Position.DistanceTo(position) < point_selection_radius)
                {
                    return (i, HandleType.Point);
                }

                // Check in-handle
                if ((bp.Position + bp.InHandle).DistanceTo(position) < handle_selection_radius)
                {
                    return (i, HandleType.InHandle);
                }

                // Check out-handle
                if ((bp.Position + bp.OutHandle).DistanceTo(position) < handle_selection_radius)
                {
                    return (i, HandleType.OutHandle);
                }
            }
            return (-1, HandleType.None);
        }

        private void handleRightClick()
        {
            ref var activeMeshComponent = ref editor.LayerManager.ActiveMesh.GetComponent<MeshComponent>();
            if (activeMeshComponent.SelectedBezierPointIndex != -1)
            {
                removeBezierPointAndTriangles(ref activeMeshComponent, activeMeshComponent.SelectedBezierPointIndex);
                activeMeshComponent.SelectedBezierPointIndex = -1;
                activeMeshComponent.SelectedHandleType = HandleType.None;
                editor.LayerManager.UpdateTriangulationForMesh(activeMeshComponent); // Use LayerManager's triangulation with MeshComponent
            }
            editor.LayerManager.NotifyMeshSelectionChanged(); // Notify UI about selection change
        }

        private void removeBezierPointAndTriangles(ref MeshComponent component, int pointIndex)
        {
            component.BezierPoints.RemoveAt(pointIndex);
        }

        private void handleDragStart()
        {
            // Logic to execute when dragging starts, if any.
        }

        public override void _Process(double delta)
        {
            if (stateMachine.State != State.Dragging) return;
            ref var activeMeshComponent = ref editor.LayerManager.ActiveMesh.GetComponent<MeshComponent>();

            if (activeMeshComponent.SelectedBezierPointIndex == -1) return;

            BezierPoint currentPoint = activeMeshComponent.BezierPoints[activeMeshComponent.SelectedBezierPointIndex];
            Vector2 mousePosition = GetGlobalMousePosition();

            switch (activeMeshComponent.SelectedHandleType)
            {
                case HandleType.Point:
                    // FIX: Removed lines that incorrectly moved handles relative to the point.
                    // Handles are offsets, so their absolute position changes automatically with the point's position.
                    currentPoint.Position = mousePosition;
                    break;
                case HandleType.InHandle:
                    currentPoint.InHandle = mousePosition - currentPoint.Position;
                    if (currentPoint.HandlesLocked) // Locking logic
                    {
                        currentPoint.OutHandle = -currentPoint.InHandle;
                    }
                    break;
                case HandleType.OutHandle:
                    currentPoint.OutHandle = mousePosition - currentPoint.Position;
                    if (currentPoint.HandlesLocked) // Locking logic
                    {
                        currentPoint.InHandle = -currentPoint.OutHandle;
                    }
                    break;
                case HandleType.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            activeMeshComponent.BezierPoints[activeMeshComponent.SelectedBezierPointIndex] = currentPoint;
            editor.LayerManager.UpdateTriangulationForMesh(activeMeshComponent); // Use LayerManager's triangulation with MeshComponent
            editor.LayerManager.NotifyMeshSelectionChanged(); // Notify UI about selection change during drag
        }
    }
}
