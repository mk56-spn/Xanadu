using System.Collections.Generic;
using Godot;
using Godot.Collections;

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class MeshRenderNode(IMeshEditor editor) : Node2D
    {
        public override void _Process(double delta)
        {
            QueueRedraw();
        }

        public override void _Draw()
        {
            bool showAllLayers = editor.LayerManager.GetShowAllLayers();

            if (showAllLayers)
            {
                var allMeshEntities = editor.LayerManager.GetAllMeshEntities();
                foreach (var entity in allMeshEntities)
                {
                    if (entity == default) continue;
                    ref var meshData = ref entity.GetComponent<MeshComponent>();
                    // Pass whether this specific entity is the active one
                    drawMeshLayer(meshData, entity == editor.LayerManager.ActiveMesh);
                }
            }
            else
            {
                if (editor.LayerManager.ActiveMesh == default) return;

                ref var meshData = ref  editor.LayerManager.ActiveMesh.GetComponent<MeshComponent>();
                drawMeshLayer(meshData, true); // Always true for the single active mesh
            }
        }

        private void drawMeshLayer(MeshComponent meshComponent, bool isActiveMesh)
        {

            // Draw Bezier curve segments (lines)
            if (meshComponent.BezierPoints.Count >= 1)
            {
                List<Vector2> curvePoints = new List<Vector2>();
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
                {
                    for (int i = 0; i < curvePoints.Count - 1; i++)
                    {
                        DrawLine(curvePoints[i], curvePoints[i + 1], Colors.Red, 2);
                    }
                }
            }

            // Draw Bezier points and handles only for the active mesh - This is correct
            if (isActiveMesh)
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
                    DrawCircle(bp.Position, 5, pointColor);

                    // In-handle
                    Vector2 inHandlePos = bp.Position + bp.InHandle;
                    Color inHandleColor = Colors.Yellow;
                    if (i == meshComponent.SelectedBezierPointIndex && meshComponent.SelectedHandleType == HandleType.InHandle)
                    {
                        inHandleColor = Colors.Red;
                    }
                    DrawLine(bp.Position, inHandlePos, Colors.Gray, 1);
                    DrawRect(new Rect2(inHandlePos - new Vector2(3, 3), new Vector2(6, 6)), inHandleColor);

                    // Out-handle
                    Vector2 outHandlePos = bp.Position + bp.OutHandle;
                    Color outHandleColor = Colors.Yellow;
                    if (i == meshComponent.SelectedBezierPointIndex && meshComponent.SelectedHandleType == HandleType.OutHandle)
                    {
                        outHandleColor = Colors.Red;
                    }
                    DrawLine(bp.Position, outHandlePos, Colors.Gray, 1);
                    DrawRect(new Rect2(outHandlePos - new Vector2(3, 3), new Vector2(6, 6)), outHandleColor);
                }
            }
        }
    }
}
