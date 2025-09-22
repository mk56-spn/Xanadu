using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Factories;

namespace XanaduProject.Scenes.ItemEditor.Systems
{
    public class MeshOutlineRenderSystem(IItemEditor editor) : QuerySystem<MeshComponent>
    {
        private readonly RenderRid canvas = RenderRid.Create();
        private readonly List<Vector2> curvePoints = [];
        private static readonly Color[] line_color = [Colors.Red];

        protected override void OnAddStore(EntityStore store)
        {
            canvas.SetParent(editor.CanvasRid);
        }

        private void drawMeshLayer(MeshComponent meshComponent, bool isActiveMesh)
        {
            // Clear the RenderRid before drawing
            // Draw Bezier curve segments (lines)
            if (meshComponent.BezierPoints.Count >= 1)
            {
                curvePoints.Clear();
                int segmentsPerCurve = 20;

                for (int i = 0; i < meshComponent.BezierPoints.Count; i++)
                {
                    BezierPoint p1 = meshComponent.BezierPoints[i];
                    BezierPoint p2 = meshComponent.BezierPoints[(i + 1) % meshComponent.BezierPoints.Count];

                    for (int j = 0; j <= segmentsPerCurve; j++)
                    {
                        float t = (float)j / segmentsPerCurve;
                        Vector2 point = p1.Position.BezierInterpolate(p1.Position + p1.OutHandle, p2.Position + p2.InHandle, p2.Position, t);
                        curvePoints.Add(point);
                    }
                }

                if (curvePoints.Count > 1)
                    canvas.AddPolyline(curvePoints.ToArray(), line_color, 2);
            }

            // Draw Bezier points and handles only for the active mesh - This is correct
            if (!isActiveMesh) return;
            {
                for (int i = 0; i < meshComponent.BezierPoints.Count; i++)
                {
                    BezierPoint bp = meshComponent.BezierPoints[i];

                    // Main point
                    Color pointColor = Colors.White;
                    if (i == meshComponent.SelectedBezierPointIndex && meshComponent.SelectedHandleType == HandleType.Point)
                    {
                        pointColor = Colors.Red;
                    }
                    canvas.AddCircle( 5,bp.Position, pointColor);

                    // In-handle
                    Vector2 inHandlePos = bp.Position + bp.InHandle;
                    Color inHandleColor = Colors.Yellow;
                    if (i == meshComponent.SelectedBezierPointIndex && meshComponent.SelectedHandleType == HandleType.InHandle)
                    {
                        inHandleColor = Colors.Red;
                    }
                    canvas.AddLine(bp.Position, inHandlePos, Colors.Gray);
                    canvas.AddRect(new Rect2(inHandlePos - new Vector2(3, 3), new Vector2(6, 6)), inHandleColor);

                    // Out-handle
                    Vector2 outHandlePos = bp.Position + bp.OutHandle;
                    Color outHandleColor = Colors.Yellow;
                    if (i == meshComponent.SelectedBezierPointIndex && meshComponent.SelectedHandleType == HandleType.OutHandle)
                    {
                        outHandleColor = Colors.Red;
                    }
                    canvas.AddLine(bp.Position, outHandlePos, Colors.Gray);
                    canvas.AddRect(new Rect2(outHandlePos - new Vector2(3, 3), new Vector2(6, 6)), outHandleColor);
                }
            }
        }

        protected override void OnUpdate()
        {
            canvas.Clear();
            bool showAllLayers = editor.LayerManager.GetShowAllLayers();

            if (showAllLayers)
            {
                var allLayerEntities = editor.LayerManager.GetAllLayerEntities();
                foreach (var entity in allLayerEntities)
                {
                    if (entity.TryGetComponent(out MeshComponent meshData))
                    {
                        // Pass whether this specific entity is the active one
                        drawMeshLayer(meshData, entity == editor.LayerManager.ActiveEntity);
                    }
                }
            }
            else
            {
                if (editor.LayerManager.ActiveEntity == default) return;

                if (editor.LayerManager.ActiveEntity.TryGetComponent(out MeshComponent meshData))
                {
                    drawMeshLayer(meshData, true); // Always true for the single active mesh
                }
            }
        }
    }
}
