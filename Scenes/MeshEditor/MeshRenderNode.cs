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
                    var meshData = entity.GetComponent<MeshComponent>().MeshData;
                    // Pass whether this specific entity is the active one
                    DrawMeshLayer(meshData, entity == editor.LayerManager.ActiveMesh);
                }
            }
            else
            {
#pragma warning disable CS8073 // The result of the expression is always the same since a value of this type is never equal to 'null'
                if (editor.LayerManager.ActiveMesh == null)
#pragma warning restore CS8073 // The result of the expression is always the same since a value of this type is never equal to 'null'
                {
                    return; // Nothing to draw if no active mesh
                }
                var meshData = editor.LayerManager.ActiveMesh.GetComponent<MeshComponent>().MeshData;
                DrawMeshLayer(meshData, true); // Always true for the single active mesh
            }
        }

        private void DrawMeshLayer(MeshData meshData, bool isActiveMesh)
        {
            // Polygon drawing is now handled by the RenderRid in MeshLayerManager, so remove this section.
            // if (meshData.Triangles.Count > 0)
            // {
            //     List<Vector2> sampledPoints = new List<Vector2>();
            //     int segmentsPerCurve = 10;
            //
            //     if (meshData.BezierPoints.Count >= 1)
            //     {
            //         for (int i = 0; i < meshData.BezierPoints.Count; i++)
            //         {
            //             BezierPoint p1 = meshData.BezierPoints[i];
            //             BezierPoint p2 = meshData.BezierPoints[(i + 1) % meshData.BezierPoints.Count];
            //
            //             for (int j = 0; j < segmentsPerCurve; j++)
            //             {
            //                 float t = (float)j / segmentsPerCurve;
            //                 Vector2 point = p1.Position.BezierInterpolate(p1.Position + p1.OutHandle, p2.Position + p2.InHandle, p2.Position, t);
            //                 sampledPoints.Add(point);
            //             }
            //         }
            //     }
            //
            //     if (sampledPoints.Count >= 3)
            //     {
            //         var arrays = new Array();
            //         arrays.Resize((int)Mesh.ArrayType.Max);
            //         arrays[(int)Mesh.ArrayType.Vertex] = sampledPoints.ToArray();
            //         arrays[(int)Mesh.ArrayType.Index] = meshData.Triangles.ToArray();
            //
            //         ArrayMesh currentLayerArrayMesh = new ArrayMesh();
            //         currentLayerArrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            //
            //         DrawMesh(currentLayerArrayMesh, null, null);
            //     }
            // }

            // Draw Bezier curve segments (lines)
            if (meshData.BezierPoints.Count >= 1)
            {
                List<Vector2> curvePoints = new List<Vector2>();
                int segmentsPerCurve = 20;

                for (int i = 0; i < meshData.BezierPoints.Count; i++)
                {
                    BezierPoint p1 = meshData.BezierPoints[i];
                    BezierPoint p2 = meshData.BezierPoints[(i + 1) % meshData.BezierPoints.Count];

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
                for (int i = 0; i < meshData.BezierPoints.Count; i++)
                {
                    BezierPoint bp = meshData.BezierPoints[i];

                    // Main point
                    Color pointColor = Colors.White;
                    if (i == meshData.SelectedBezierPointIndex && meshData.SelectedHandleType == HandleType.Point)
                    {
                        pointColor = Colors.Red;
                    }
                    DrawCircle(bp.Position, 5, pointColor);

                    // In-handle
                    Vector2 inHandlePos = bp.Position + bp.InHandle;
                    Color inHandleColor = Colors.Yellow;
                    if (i == meshData.SelectedBezierPointIndex && meshData.SelectedHandleType == HandleType.InHandle)
                    {
                        inHandleColor = Colors.Red;
                    }
                    DrawLine(bp.Position, inHandlePos, Colors.Gray, 1);
                    DrawRect(new Rect2(inHandlePos - new Vector2(3, 3), new Vector2(6, 6)), inHandleColor);

                    // Out-handle
                    Vector2 outHandlePos = bp.Position + bp.OutHandle;
                    Color outHandleColor = Colors.Yellow;
                    if (i == meshData.SelectedBezierPointIndex && meshData.SelectedHandleType == HandleType.OutHandle)
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
