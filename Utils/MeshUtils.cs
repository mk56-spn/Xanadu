using XanaduProject.Factories;
using XanaduProject.Scenes.ItemEditor;

namespace XanaduProject.Utils
{
    public static class MeshUtils
    {
        public static void UpdateTriangulation(MeshComponent meshComponent)
        {
            meshComponent.RenderRid.Clear();

            if (meshComponent.BezierPoints.Count < 3) return;

            var (vertices, indices) = BezierTriangulator.Triangulate(meshComponent.BezierPoints);

            if (vertices.Count < 3 || indices.Count <= 0) return;

            meshComponent.RenderRid.AddTriangleArray(vertices, indices, meshComponent.Color);
        }
    }
}
