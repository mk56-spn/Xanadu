using Friflo.Engine.ECS;
using Godot;
using Stateless;
using XanaduProject.GameDependencies;
using System.Collections.Generic;
using System.Linq;

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
                .Permit(Trigger.LeftDown, State.Pressed)
                .Ignore(Trigger.RightDown);

            stateMachine.Configure(State.Pressed)
                .OnEntry(HandlePress)
                .Permit(Trigger.LeftUp, State.Idle)
                .Permit(Trigger.MouseMoved, State.Dragging)
                .Permit(Trigger.RightDown, State.Idle);

            stateMachine.Configure(State.Dragging)
                .OnEntry(HandleDragStart)
                .Permit(Trigger.LeftUp, State.Idle)
                .Permit(Trigger.RightDown, State.Idle)
                .OnExit(HandleDragEnd);

            stateMachine.OnTransitioned(transition =>
            {
                if (transition.Trigger == Trigger.RightDown) HandleRightClick();
            });
        }

        public override void _Input(InputEvent @event)
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

        private void HandlePress()
        {
            pressPosition = GetGlobalMousePosition();
            var meshData = editor.MeshEntity.GetComponent<MeshComponent>().MeshData;

            (int clickedPointIndex, HandleType clickedHandleType) = FindClickedBezierElement(pressPosition);

            if (clickedPointIndex != -1)
            {
                meshData.SelectedBezierPointIndex = clickedPointIndex;
                meshData.SelectedHandleType = clickedHandleType;
            }
            else
            {
                // Add new Bezier point
                meshData.BezierPoints.Add(new BezierPoint(pressPosition));
                meshData.SelectedBezierPointIndex = meshData.BezierPoints.Count - 1;
                meshData.SelectedHandleType = HandleType.Point;
                UpdateTriangulation();
            }
        }

        private (int, HandleType) FindClickedBezierElement(Vector2 position)
        {
            var meshData = editor.MeshEntity.GetComponent<MeshComponent>().MeshData;

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

        private void HandleRightClick()
        {
            var meshData = editor.MeshEntity.GetComponent<MeshComponent>().MeshData;
            if (meshData.SelectedBezierPointIndex != -1)
            {
                RemoveBezierPointAndTriangles(meshData, meshData.SelectedBezierPointIndex);
                meshData.SelectedBezierPointIndex = -1;
                meshData.SelectedHandleType = HandleType.None;
                UpdateTriangulation(); // Re-triangulate after removal
            }
        }

        private void RemoveBezierPointAndTriangles(MeshData meshData, int pointIndex)
        {
            meshData.BezierPoints.RemoveAt(pointIndex);

            // Always clear and re-triangulate, so no need to adjust individual triangle indices.
            meshData.Triangles.Clear();
        }

        private void HandleDragStart()
        {
            // Logic to execute when dragging starts, if any.
        }

        public override void _Process(double delta)
        {
            if (stateMachine.State == State.Dragging)
            {
                var meshData = editor.MeshEntity.GetComponent<MeshComponent>().MeshData;
                if (meshData.SelectedBezierPointIndex != -1)
                {
                    BezierPoint currentPoint = meshData.BezierPoints[meshData.SelectedBezierPointIndex];
                    Vector2 mousePosition = GetGlobalMousePosition();

                    switch (meshData.SelectedHandleType)
                    {
                        case HandleType.Point:
                            Vector2 deltaMove = mousePosition - currentPoint.Position;
                            currentPoint.Position = mousePosition;
                            // Move handles with the point
                            currentPoint.InHandle += deltaMove;
                            currentPoint.OutHandle += deltaMove;
                            break;
                        case HandleType.InHandle:
                            currentPoint.InHandle = mousePosition - currentPoint.Position;
                            break;
                        case HandleType.OutHandle:
                            currentPoint.OutHandle = mousePosition - currentPoint.Position;
                            break;
                    }
                    meshData.BezierPoints[meshData.SelectedBezierPointIndex] = currentPoint;
                    UpdateTriangulation(); // Re-triangulate when dragging to update mesh shape
                }
            }
        }

        private void HandleDragEnd()
        {
            // Logic to execute when dragging ends, if any.
        }

        private void UpdateTriangulation()
        {
            var meshData = editor.MeshEntity.GetComponent<MeshComponent>().MeshData;
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
                    var indices = Geometry2D.TriangulatePolygon(sampledPoints.ToArray());
                    meshData.Triangles.AddRange(indices);
                }
            }
        }
    }
}
