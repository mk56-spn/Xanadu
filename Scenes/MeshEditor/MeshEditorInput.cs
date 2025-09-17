using Godot;
using Stateless;
using System.Collections.Generic;

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
                .Permit(Trigger.RightDown, State.Idle)
                .OnExit(handleDragEnd);

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
            var meshData = editor.LayerManager.ActiveMesh.GetComponent<MeshComponent>().MeshData;

            (int clickedPointIndex, HandleType clickedHandleType) = findClickedBezierElement(pressPosition);

            if (clickedPointIndex != -1)
            {
                meshData.SelectedBezierPointIndex = clickedPointIndex;
                meshData.SelectedHandleType = clickedHandleType;
            }
            else
            {
                // Add new Bezier point
                BezierPoint newPoint = new BezierPoint(pressPosition);
                newPoint.HandlesLocked = false; // Default to unlocked
                meshData.BezierPoints.Add(newPoint);
                meshData.SelectedBezierPointIndex = meshData.BezierPoints.Count - 1;
                meshData.SelectedHandleType = HandleType.Point;
                updateTriangulation();
            }
            editor.LayerManager.NotifyMeshSelectionChanged(); // Notify UI about selection change
        }

        private (int, HandleType) findClickedBezierElement(Vector2 position)
        {
            var meshData = editor.LayerManager.ActiveMesh.GetComponent<MeshComponent>().MeshData;

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
            GD.Print("handleRightClick called.");
            var meshData = editor.LayerManager.ActiveMesh.GetComponent<MeshComponent>().MeshData;
            GD.Print($"SelectedBezierPointIndex: {meshData.SelectedBezierPointIndex}");
            if (meshData.SelectedBezierPointIndex != -1)
            {
                removeBezierPointAndTriangles(meshData, meshData.SelectedBezierPointIndex);
                meshData.SelectedBezierPointIndex = -1;
                meshData.SelectedHandleType = HandleType.None;
                updateTriangulation(); // Re-triangulate after removal
            }
            editor.LayerManager.NotifyMeshSelectionChanged(); // Notify UI about selection change
        }

        private void removeBezierPointAndTriangles(MeshData meshData, int pointIndex)
        {
            meshData.BezierPoints.RemoveAt(pointIndex);

            // Always clear and re-triangulate, so no need to adjust individual triangle indices.
            meshData.Triangles.Clear();
        }

        private void handleDragStart()
        {
            // Logic to execute when dragging starts, if any.
        }

        public override void _Process(double delta)
        {
            if (stateMachine.State == State.Dragging)
            {
                var meshData = editor.LayerManager.ActiveMesh.GetComponent<MeshComponent>().MeshData;
                if (meshData.SelectedBezierPointIndex != -1)
                {
                    BezierPoint currentPoint = meshData.BezierPoints[meshData.SelectedBezierPointIndex];
                    Vector2 mousePosition = GetGlobalMousePosition();

                    switch (meshData.SelectedHandleType)
                    {
                        case HandleType.Point:
                            // FIX: Removed lines that incorrectly moved handles relative to the point.
                            // Handles are offsets, so their absolute position changes automatically with the point\'s position.
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
                    }
                    meshData.BezierPoints[meshData.SelectedBezierPointIndex] = currentPoint;
                    updateTriangulation(); // Re-triangulate when dragging to update mesh shape
                    editor.LayerManager.NotifyMeshSelectionChanged(); // Notify UI about selection change during drag
                }
            }
        }

        private void handleDragEnd()
        {
            // Logic to execute when dragging ends, if any.
        }

        private void updateTriangulation()
        {
            var meshData = editor.LayerManager.ActiveMesh.GetComponent<MeshComponent>().MeshData;
            meshData.Triangles.Clear();

            if (meshData.BezierPoints.Count >= 3)
            {
                // Generate sampled points from the Bezier curve for triangulation
                List<Vector2> sampledPoints = new List<Vector2>();
                int segmentsPerCurve = 10; // Number of linear segments to approximate each Bezier curve

                for (int i = 0; i < meshData.BezierPoints.Count; i++)
                {
                    BezierPoint p1 = meshData.BezierPoints[i];
                    BezierPoint p2 = meshData.BezierPoints[(i + 1) % meshData.BezierPoints.Count]; // Wrap around for closed curve

                    for (int j = 0; j < segmentsPerCurve; j++)
                    {
                        float t = (float)j / segmentsPerCurve;
                        Vector2 point = p1.Position.BezierInterpolate(p1.Position + p1.OutHandle, p2.Position + p2.InHandle, p2.Position, t);
                        sampledPoints.Add(point);
                    }
                }

                if (sampledPoints.Count >= 3)
                {
                    // Use TriangulatePolygon for correct polygon triangulation
                    int[]? indices = Geometry2D.TriangulatePolygon(sampledPoints.ToArray());
                    meshData.Triangles.AddRange(indices);
                }
            }
        }
    }
}
