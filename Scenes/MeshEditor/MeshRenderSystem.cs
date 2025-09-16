using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Godot.Collections;
using XanaduProject.GameDependencies;

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class MeshRenderSystem(IMeshEditor editor) : Node2D
    {
        private readonly EntityStore entityStore = DiProvider.Get<EntityStore>();
        private readonly ArrayMesh arrayMesh = new();

        public override void _Process(double delta)
        {
            QueueRedraw();
        }

        public override void _Draw()
        {
            var meshData = editor.MeshEntity.GetComponent<MeshComponent>().MeshData;

            // Draw the filled mesh (triangulation from sampled Bezier points)
            if (meshData.Triangles.Count > 0)
            {
                // We need to get the sampled points from MeshEditorInput's UpdateTriangulation logic
                // For now, we'll re-sample here for rendering, but ideally this would be cached.
                List<Vector2> sampledPoints = new List<Vector2>();
                int segmentsPerCurve = 10; // Must match MeshEditorInput's sampling

                if (meshData.BezierPoints.Count >= 1)
                {
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
                }

                if (sampledPoints.Count >= 3)
                {
                    var arrays = new Godot.Collections.Array();
                    arrays.Resize((int)Mesh.ArrayType.Max);
                    arrays[(int)Mesh.ArrayType.Vertex] = sampledPoints.ToArray();
                    arrays[(int)Mesh.ArrayType.Index] = meshData.Triangles.ToArray();

                    arrayMesh.ClearSurfaces();
                    arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

                    // Draw the filled mesh with a solid color
                    DrawMesh(arrayMesh, null, null);
                }
            }

            // Draw Bezier curve segments (lines)
            if (meshData.BezierPoints.Count >= 1)
            {
                List<Vector2> curvePoints = new List<Vector2>();
                int segmentsPerCurve = 20; // Higher resolution for drawing the curve itself

                for (int i = 0; i < meshData.BezierPoints.Count; i++)
                {
                    BezierPoint p1 = meshData.BezierPoints[i];
                    BezierPoint p2 = meshData.BezierPoints[(i + 1) % meshData.BezierPoints.Count]; // Wrap around for closed curve

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

            // Draw Bezier points and handles
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
