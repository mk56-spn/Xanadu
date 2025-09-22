using System.Collections.Generic;
using System.Linq;
using Godot;
using XanaduProject.Scenes.Editor.Input;
using XanaduProject.Utils;

namespace XanaduProject.Scenes.ItemEditor
{
    public partial class ItemEditorInput : BaseInputHandler
    {
        private const float point_selection_radius = 10f;
        private const float handle_selection_radius = 8f;
        private const float same_spot_threshold = 2f;

        protected override float DragThreshold => 5f;

        private readonly IItemEditor editor;
        private Vector2 pressPosition;

        // Fields for cycling
        private List<(int, HandleType)> lastFoundElements = new();
        private int lastSelectionCycleIndex;
        private Vector2 lastCyclePosition;
        private List<(int, HandleType)> selectedElements = new();


        public ItemEditorInput(IItemEditor editor)
        {
            this.editor = editor;
            Ready += () => SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        public override void _GuiInput(InputEvent @event)
        {
            ProcessInput(@event);
        }

        protected override void HandleLeftPress(bool multiSelect)
        {
            pressPosition = getWorldMousePosition();
            var activeEntity = editor.LayerManager.ActiveEntity;

            if (activeEntity == default || !activeEntity.HasComponent<MeshComponent>())
            {
                // No active mesh or active entity doesn't have a MeshComponent, so nothing to do.
                return;
            }

            // Get a reference to the MeshComponent to ensure modifications are applied to the original
            ref MeshComponent activeMeshComponent = ref activeEntity.GetComponent<MeshComponent>();

            var clickedElements = findClickedBezierElements(pressPosition);

            if (multiSelect)
            {
                if (clickedElements.Count > 0)
                {
                    var topElement = clickedElements[0]; // findClickedBezierElements prioritizes handles
                    if (!selectedElements.Contains(topElement))
                    {
                        selectedElements.Add(topElement);
                    }
                }
            }
            else // single select
            {
                if (clickedElements.Count == 0)
                {
                    // if nothing clicked, clear selection OR create new point
                    if (selectedElements.Count > 0)
                    {
                        selectedElements.Clear();
                    }
                    else
                    {
                        // Create new point logic
                        var points = activeMeshComponent.BezierPoints;
                        var newPoint = new BezierPoint(pressPosition) { HandlesLocked = false };
                        int insertionIndex;

                        if (points.Count < 2)
                        {
                            if (points.Count > 0) // This is for 1 point
                            {
                                var prevPoint = points[^1];
                                var inHandleDir = (prevPoint.Position - newPoint.Position).Normalized();
                                float inHandleLength = (prevPoint.Position - newPoint.Position).Length() / 3f;
                                newPoint.InHandle = inHandleDir * inHandleLength;

                                var nextPoint = points[0];
                                var outHandleDir = (nextPoint.Position - newPoint.Position).Normalized();
                                float outHandleLength = (nextPoint.Position - newPoint.Position).Length() / 3f;
                                newPoint.OutHandle = outHandleDir * outHandleLength;
                            }

                            insertionIndex = points.Count;
                            activeMeshComponent.BezierPoints.Add(newPoint);
                        }
                        else
                        {
                            // New logic for inserting between points
                            insertionIndex = FindClosestSegmentIndex(pressPosition, points) + 1;

                            var prevPoint = points[insertionIndex - 1];
                            var nextPoint = points[insertionIndex % points.Count];

                            var inHandleDir = (prevPoint.Position - newPoint.Position).Normalized();
                            float inHandleLength = (prevPoint.Position - newPoint.Position).Length() / 3f;
                            newPoint.InHandle = inHandleDir * inHandleLength;

                            var outHandleDir = (nextPoint.Position - newPoint.Position).Normalized();
                            float outHandleLength = (nextPoint.Position - newPoint.Position).Length() / 3f;
                            newPoint.OutHandle = outHandleDir * outHandleLength;

                            activeMeshComponent.BezierPoints.Insert(insertionIndex, newPoint);
                        }

                        var newElement = (insertionIndex, HandleType.Point);
                        selectedElements.Add(newElement);
                        MeshUtils.UpdateTriangulation(activeMeshComponent);
                    }
                }
                else // something clicked
                {
                    // Cycling logic
                    if (lastFoundElements.Count > 1 &&
                        GetLocalMousePosition().DistanceTo(lastCyclePosition) < same_spot_threshold)
                    {
                        lastSelectionCycleIndex = (lastSelectionCycleIndex + 1) % lastFoundElements.Count;
                    }
                    else
                    {
                        lastFoundElements = clickedElements;
                        lastSelectionCycleIndex = 0;
                        lastCyclePosition = GetLocalMousePosition();
                    }

                    var currentElement = lastFoundElements[lastSelectionCycleIndex];

                    // if it's not the only thing selected, clear and select it.
                    if (selectedElements.Count != 1 || selectedElements[0] != currentElement)
                    {
                        selectedElements.Clear();
                        selectedElements.Add(currentElement);
                    }
                }
            }

            // Update the singular selection properties for visual feedback on the "active" element
            if (selectedElements.Count > 0)
            {
                (activeMeshComponent.SelectedBezierPointIndex, activeMeshComponent.SelectedHandleType) = selectedElements[^1];
            }
            else
            {
                activeMeshComponent.SelectedBezierPointIndex = -1;
                activeMeshComponent.SelectedHandleType = HandleType.None;
            }

            editor.LayerManager.NotifySelectionChanged();
        }

        protected override void OnRightClick()
        {
            var activeEntity = editor.LayerManager.ActiveEntity;
            if (activeEntity == default || !activeEntity.HasComponent<MeshComponent>())
            {
                return;
            }

            ref MeshComponent activeMeshComponent = ref activeEntity.GetComponent<MeshComponent>();

            if (selectedElements.Count == 0) return;

            // We can only delete points, not handles.
            var pointsToDelete = selectedElements.Where(e => e.Item2 == HandleType.Point).Select(e => e.Item1).Distinct()
                .OrderByDescending(i => i).ToList();

            if (pointsToDelete.Count > 0)
            {
                foreach (var index in pointsToDelete)
                {
                    activeMeshComponent.BezierPoints.RemoveAt(index);
                }

                MeshUtils.UpdateTriangulation(activeMeshComponent);
            }

            selectedElements.Clear();
            activeMeshComponent.SelectedBezierPointIndex = -1;
            activeMeshComponent.SelectedHandleType = HandleType.None;
            editor.LayerManager.NotifySelectionChanged();
        }

        protected override void OnDrag(Vector2 delta)
        {
            var activeEntity = editor.LayerManager.ActiveEntity;
            if (activeEntity == default || !activeEntity.HasComponent<MeshComponent>())
            {
                return;
            }

            ref MeshComponent activeMeshComponent = ref activeEntity.GetComponent<MeshComponent>();

            if (selectedElements.Count == 0) return;

            var activeHandleType = activeMeshComponent.SelectedHandleType;

            // When dragging a point, move all selected points.
            if (activeHandleType == HandleType.Point)
            {
                foreach (var (pointIndex, handleType) in selectedElements)
                {
                    if (handleType == HandleType.Point)
                    {
                        var p = activeMeshComponent.BezierPoints[pointIndex];
                        p.Position += delta;
                        activeMeshComponent.BezierPoints[pointIndex] = p;
                    }
                }
            }
            // When dragging a handle, move all selected handles of the same type.
            else if (activeHandleType is HandleType.InHandle or HandleType.OutHandle)
            {
                foreach (var (pointIndex, handleType) in selectedElements)
                {
                    if (handleType == activeHandleType)
                    {
                        var p = activeMeshComponent.BezierPoints[pointIndex];
                        if (handleType == HandleType.InHandle)
                        {
                            p.InHandle += delta;
                            if (p.HandlesLocked) p.OutHandle = -p.InHandle;
                        }
                        else // OutHandle
                        {
                            p.OutHandle += delta;
                            if (p.HandlesLocked) p.InHandle = -p.OutHandle;
                        }

                        activeMeshComponent.BezierPoints[pointIndex] = p;
                    }
                }
            }

            MeshUtils.UpdateTriangulation(activeMeshComponent);
            editor.LayerManager.NotifySelectionChanged();
        }

        private int FindClosestSegmentIndex(Vector2 position, IReadOnlyList<BezierPoint> points)
        {
            if (points.Count == 0) return -1;
            if (points.Count == 1) return 0;

            float minDistanceSq = float.MaxValue;
            int closestSegmentStartIndex = 0;

            for (int i = 0; i < points.Count; i++)
            {
                var p1 = points[i].Position;
                var p2 = points[(i + 1) % points.Count].Position;

                var l2 = p1.DistanceSquaredTo(p2);
                if (l2 == 0.0)
                {
                    float distanceSq = position.DistanceSquaredTo(p1);
                    if (distanceSq < minDistanceSq)
                    {
                        minDistanceSq = distanceSq;
                        closestSegmentStartIndex = i;
                    }

                    continue;
                }

                var t = Mathf.Max(0, Mathf.Min(1, (position - p1).Dot(p2 - p1) / l2));
                var projection = p1 + t * (p2 - p1);

                float distSqToSegment = position.DistanceSquaredTo(projection);

                if (distSqToSegment < minDistanceSq)
                {
                    minDistanceSq = distSqToSegment;
                    closestSegmentStartIndex = i;
                }
            }

            return closestSegmentStartIndex;
        }

        private Vector2 getWorldMousePosition() =>
            GetViewport().GetCanvasTransform().AffineInverse() * GetLocalMousePosition();

        private List<(int, HandleType)> findClickedBezierElements(Vector2 position)
        {
            var foundElements = new List<(int, HandleType)>();
            var activeEntity = editor.LayerManager.ActiveEntity;

            if (activeEntity == default || !activeEntity.TryGetComponent(out MeshComponent meshData))
            {
                return foundElements; // Return empty list if no active mesh component
            }

            // Temporarily use fixed radii for debugging selection
            float fixedPointRadius = point_selection_radius;
            float fixedHandleRadius = handle_selection_radius;

            // Prioritize handles to solve the user's problem
            for (int i = 0; i < meshData.BezierPoints.Count; i++)
            {
                BezierPoint bp = meshData.BezierPoints[i];
                if ((bp.Position + bp.InHandle).DistanceTo(position) < fixedHandleRadius)
                {
                    foundElements.Add((i, HandleType.InHandle));
                }

                if ((bp.Position + bp.OutHandle).DistanceTo(position) < fixedHandleRadius)
                {
                    foundElements.Add((i, HandleType.OutHandle));
                }
            }

            for (int i = 0; i < meshData.BezierPoints.Count; i++)
            {
                BezierPoint bp = meshData.BezierPoints[i];
                if (bp.Position.DistanceTo(position) < fixedPointRadius)
                {
                    foundElements.Add((i, HandleType.Point));
                }
            }

            return foundElements;
        }
    }
}
