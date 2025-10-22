using System.Collections.Generic;
using System.Linq;
using Godot;
using XanaduProject.Scenes.ItemEditor;
using XanaduProject.Utils;
using BaseInputHandler = XanaduProject.Screens.AssetCreation.Editor.Input.BaseInputHandler;

namespace XanaduProject.Screens.AssetCreation.ItemEditor
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
        public List<(int, HandleType)> LastFoundElements = new();
        private int lastSelectionCycleIndex;
        private Vector2 lastCyclePosition;
        private readonly List<(int, HandleType)> selectedElements = new();


        public ItemEditorInput(IItemEditor editor)
        {
            this.editor = editor;
            Ready += () => SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        protected override void HandleLeftPress(bool multiSelect)
        {
            pressPosition = editor.CanvasTransform;
            var activeEntity = editor.LayerManager.ActiveEntity;

            if (activeEntity == default || !activeEntity.HasComponent<MeshComponent>())
            {
                return;
            }

            ref MeshComponent activeMeshComponent = ref activeEntity.GetComponent<MeshComponent>();
            var clickedElements = findClickedBezierElements(pressPosition);

            if (multiSelect)
            {
                handleMultiElementSelection(clickedElements);
            }
            else
            {
                handleSingleElementSelection(clickedElements, ref activeMeshComponent);
            }

            updateSelection(ref activeMeshComponent);
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

            deleteSelectedPoints(ref activeMeshComponent);
            clearSelection(ref activeMeshComponent);
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

            switch (activeHandleType)
            {
                case HandleType.Point:
                    dragSelectedPoints(delta / 3, ref activeMeshComponent);
                    break;
                case HandleType.InHandle:
                case HandleType.OutHandle:
                    dragSelectedHandles(delta / 3, ref activeMeshComponent, activeHandleType);
                    break;
            }

            MeshUtils.UpdateTriangulation(activeMeshComponent);
            editor.LayerManager.NotifySelectionChanged();
        }

        private void handleMultiElementSelection(List<(int, HandleType)> clickedElements)
        {
            if (clickedElements.Count <= 0) return;

            var topElement = clickedElements[0]; // findClickedBezierElements prioritizes handles
            if (!selectedElements.Contains(topElement))
            {
                selectedElements.Add(topElement);
            }
        }

        private void handleSingleElementSelection(List<(int, HandleType)> clickedElements,
            ref MeshComponent activeMeshComponent)
        {
            if (clickedElements.Count == 0)
            {
                if (selectedElements.Count > 0)
                {
                    clearSelection(ref activeMeshComponent);
                }
                else
                {
                    createNewPoint(ref activeMeshComponent);
                }
            }
            else
            {
                cycleAndSelectElement(clickedElements);
            }
        }

        private void cycleAndSelectElement(List<(int, HandleType)> clickedElements)
        {
            var transformedMousePos = editor.CanvasTransform;
            if (LastFoundElements.Count > 1 &&
                transformedMousePos.DistanceTo(lastCyclePosition) < same_spot_threshold)
            {
                lastSelectionCycleIndex = (lastSelectionCycleIndex + 1) % LastFoundElements.Count;
            }
            else
            {
                LastFoundElements = clickedElements;
                lastSelectionCycleIndex = 0;
                lastCyclePosition = transformedMousePos;
            }

            var currentElement = LastFoundElements[lastSelectionCycleIndex];

            if (selectedElements.Count != 1 || selectedElements[0] != currentElement)
            {
                selectedElements.Clear();
                selectedElements.Add(currentElement);
            }
        }

        private void createNewPoint(ref MeshComponent activeMeshComponent)
        {
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
                insertionIndex = findClosestSegmentIndex(pressPosition, points) + 1;

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

            selectedElements.Add((insertionIndex, HandleType.Point));
            MeshUtils.UpdateTriangulation(activeMeshComponent);
        }

        private void dragSelectedPoints(Vector2 delta, ref MeshComponent activeMeshComponent)
        {
            foreach ((int pointIndex, var handleType) in selectedElements)
            {
                if (handleType != HandleType.Point) continue;
                var p = activeMeshComponent.BezierPoints[pointIndex];
                p.Position += delta;
                activeMeshComponent.BezierPoints[pointIndex] = p;
            }
        }

        private void dragSelectedHandles(Vector2 delta, ref MeshComponent activeMeshComponent, HandleType activeHandleType)
        {
            foreach ((int pointIndex, var handleType) in selectedElements)
            {
                if (handleType != activeHandleType) continue;

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

        private void deleteSelectedPoints(ref MeshComponent activeMeshComponent)
        {
            var pointsToDelete = selectedElements.Where(e => e.Item2 == HandleType.Point).Select(e => e.Item1)
                .Distinct().OrderByDescending(i => i).ToList();

            if (pointsToDelete.Count <= 0) return;

            foreach (int index in pointsToDelete)
            {
                activeMeshComponent.BezierPoints.RemoveAt(index);
            }

            MeshUtils.UpdateTriangulation(activeMeshComponent);
        }

        private void clearSelection(ref MeshComponent activeMeshComponent)
        {
            selectedElements.Clear();
            activeMeshComponent.SelectedBezierPointIndex = -1;
            activeMeshComponent.SelectedHandleType = HandleType.None;
        }

        private void updateSelection(ref MeshComponent activeMeshComponent)
        {
            if (selectedElements.Count > 0)
            {
                (activeMeshComponent.SelectedBezierPointIndex, activeMeshComponent.SelectedHandleType) =
                    selectedElements[^1];
            }
            else
            {
                activeMeshComponent.SelectedBezierPointIndex = -1;
                activeMeshComponent.SelectedHandleType = HandleType.None;
            }
        }

        private int findClosestSegmentIndex(Vector2 position, IReadOnlyList<BezierPoint> points)
        {
            if (points.Count == 0) return -1;
            if (points.Count == 1) return 0;

            float minDistanceSq = float.MaxValue;
            int closestSegmentStartIndex = 0;

            for (int i = 0; i < points.Count; i++)
            {
                var p1 = points[i].Position;
                var p2 = points[(i + 1) % points.Count].Position;

                float l2 = p1.DistanceSquaredTo(p2);
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

                float t = Mathf.Max(0, Mathf.Min(1, (position - p1).Dot(p2 - p1) / l2));
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


        private List<(int, HandleType)> findClickedBezierElements(Vector2 position)
        {
            var foundElements = new List<(int, HandleType)>();
            var activeEntity = editor.LayerManager.ActiveEntity;

            if (activeEntity == default || !activeEntity.TryGetComponent(out MeshComponent meshData))
            {
                return foundElements; // Return empty list if no active mesh component
            }

            float fixedPointRadius = point_selection_radius;
            float fixedHandleRadius = handle_selection_radius;

            // Prioritize handles
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
