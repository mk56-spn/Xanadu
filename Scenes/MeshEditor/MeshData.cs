using System.Collections.Generic;
using Godot;

namespace XanaduProject.Scenes.MeshEditor
{
    public class MeshData
    {
        public List<Vector2> Vertices { get; } = new();
        public int SelectedVertexIndex { get; set; } = -1;
        // Triangles will be added later
    }
}
