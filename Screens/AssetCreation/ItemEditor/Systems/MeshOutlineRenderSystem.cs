using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Factories;
using XanaduProject.Scenes.ItemEditor;
using static Godot.Colors;

namespace XanaduProject.Screens.AssetCreation.ItemEditor.Systems
{
    public class MeshOutlineRenderSystem(IItemEditor editor) : QuerySystem<MeshComponent>
    {
        private const int segments_per_curve = 20;
        private const int handle_size = 6;
        private const int line_width = 2;
        private const int handle_offset = 3;

        private static readonly Color[] line_colors = [Red];
        private static readonly Color default_point_color = White;
        private static readonly Color selected_point_color = Red;
        private static readonly Color default_handle_color = Yellow;
        private static readonly Color handle_line_color = Gray;

        private readonly RenderRid canvas = RenderRid.Create();
        private readonly List<Vector2> curvePoints = [];

        protected override void OnAddStore(EntityStore store)
        {
            canvas.SetParent(editor.CanvasRid);
        }

        protected override void OnUpdate()
        {
            canvas.Clear();

            if (editor.LayerManager.GetShowAllLayers())
            {
                drawAllLayers();
            }
            else
            {
                DrawActiveLayer();
            }
        }

        private void drawAllLayers()
        {
            var allLayerEntities = editor.LayerManager.GetAllLayerEntities();

            foreach (var entity in allLayerEntities)
            {
                if (entity.TryGetComponent(out MeshComponent meshData))
                {
                    drawMeshLayer(meshData, entity == editor.LayerManager.ActiveEntity);
                }
            }
        }

        private void DrawActiveLayer()
        {
            var activeEntity = editor.LayerManager.ActiveEntity;
            if (activeEntity == default)
                return;

            if (activeEntity.TryGetComponent(out MeshComponent meshData))
            {
                drawMeshLayer(meshData, true);
            }
        }

        private void drawMeshLayer(MeshComponent meshComponent, bool isActiveMesh)
        {
            drawBezierCurve(meshComponent);

            if (isActiveMesh)
            {
                drawBezierHandles(meshComponent);
            }
        }

        private void drawBezierCurve(MeshComponent meshComponent)
        {
            if (meshComponent.BezierPoints.Count < 2)
                return;

            curvePoints.Clear();

            for (int i = 0; i < meshComponent.BezierPoints.Count; i++)
            {
                var currentPoint = meshComponent.BezierPoints[i];
                var nextPoint = meshComponent.BezierPoints[(i + 1) % meshComponent.BezierPoints.Count];

                for (int j = 0; j <= segments_per_curve; j++)
                {
                    float t = (float)j / segments_per_curve;
                    Vector2 point = currentPoint.Position.BezierInterpolate(
                        currentPoint.Position + currentPoint.OutHandle,
                        nextPoint.Position + nextPoint.InHandle,
                        nextPoint.Position,
                        t
                    );
                    curvePoints.Add(point);
                }
            }

            if (curvePoints.Count > 1)
            {
                canvas.AddPolyline(curvePoints.ToArray(), line_colors, line_width);
            }
        }

        private void drawBezierHandles(MeshComponent meshComponent)
        {
            for (int i = 0; i < meshComponent.BezierPoints.Count; i++)
            {
                var bezierPoint = meshComponent.BezierPoints[i];
                bool isSelectedPoint = i == meshComponent.SelectedBezierPointIndex;

                drawMainPoint(bezierPoint, isSelectedPoint, meshComponent.SelectedHandleType);
                drawInHandle(bezierPoint, isSelectedPoint, meshComponent.SelectedHandleType);
                drawOutHandle(bezierPoint, isSelectedPoint, meshComponent.SelectedHandleType);
            }
        }

        private void drawMainPoint(BezierPoint point, bool isSelectedPoint, HandleType selectedHandleType)
        {
            var pointColor = isSelectedPoint && selectedHandleType == HandleType.Point
                ? selected_point_color
                : default_point_color;

            canvas.AddHandle(pointColor, Beige, point.Position);
        }

        private void drawInHandle(BezierPoint point, bool isSelectedPoint, HandleType selectedHandleType)
        {
            var inHandlePosition = point.Position + point.InHandle;
            var handleColor = isSelectedPoint && selectedHandleType == HandleType.InHandle
                ? selected_point_color
                : default_handle_color;

            canvas.AddLine(point.Position, inHandlePosition, handle_line_color);
            canvas.AddHandle(handleColor, Beige, inHandlePosition);
        }

        private void drawOutHandle(BezierPoint point, bool isSelectedPoint, HandleType selectedHandleType)
        {
            var outHandlePosition = point.Position + point.OutHandle;
            var handleColor = isSelectedPoint && selectedHandleType == HandleType.OutHandle
                ? selected_point_color
                : default_handle_color;

            canvas.AddLine(point.Position, outHandlePosition, handle_line_color);
            canvas.AddHandle(handleColor, Beige, outHandlePosition);
        }

        private static Rect2 createHandleRect(Vector2 position)
        {
            var offset = new Vector2(handle_offset, handle_offset);
            var size = new Vector2(handle_size, handle_size);
            return new Rect2(position - offset, size);
        }
    }
}
