using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using Godot;
using XanaduProject.Scenes.ItemEditor;

namespace XanaduProject.Screens.AssetCreation.ItemEditor
{
    public static class BezierTriangulator
    {
        private const float scale = 1000.0f;

        public static (List<Vector2> vertices, List<int> indices) Triangulate(IReadOnlyList<BezierPoint> bezierPoints, int segmentsPerCurve = 20)
        {
            if (bezierPoints.Count < 2)
            {
                return ([], []);
            }

            List<Vector2> sampledPoints = sampleBezierCurve(bezierPoints, segmentsPerCurve);

            if (sampledPoints.Count < 2)
            {
                return ([], []);
            }

            Paths64 subject = [];
            Path64 path = [];

            path.AddRange(sampledPoints.Select(pt => new Point64(pt.X * scale, pt.Y * scale)));
            subject.Add(path);

            Paths64 solution = [];
            Clipper64 clipper = new Clipper64();
            clipper.AddSubject(subject);
            clipper.Execute(ClipType.Union, FillRule.NonZero, solution);

            var allVertices = new List<Vector2>();
            var allIndices = new List<int>();

            foreach (var polygonPath in solution)
            {
                var polygon = polygonPath.Select(pt => new Vector2(pt.X / scale, pt.Y / scale)).ToList();

                if (polygon.Count < 3) continue;

                int vertexOffset = allVertices.Count;
                allVertices.AddRange(polygon);

                int[] newIndices = Geometry2D.TriangulatePolygon(polygon.ToArray());

                allIndices.AddRange(newIndices.Select(index => vertexOffset + index));
            }

            return (allVertices, allIndices);
        }

        private static List<Vector2> sampleBezierCurve(IReadOnlyList<BezierPoint> bezierPoints, int segmentsPerCurve)
        {
            List<Vector2> sampledPoints = [];

            for (int i = 0; i < bezierPoints.Count; i++)
            {
                BezierPoint p1 = bezierPoints[i];
                BezierPoint p2 = bezierPoints[(i + 1) % bezierPoints.Count]; // Wrap around for closed curve

                for (int j = 0; j < segmentsPerCurve; j++)
                {
                    float t = (float)j / segmentsPerCurve;
                    Vector2 point = p1.Position.BezierInterpolate(p1.Position + p1.OutHandle, p2.Position + p2.InHandle, p2.Position, t);
                    sampledPoints.Add(point);
                }
            }
            return sampledPoints;
        }
    }
}
