using System.Collections.Generic;
using Godot;

namespace XanaduProject.Scenes.MeshEditor
{
    public struct BezierPoint(Vector2 position)
    {
        public bool HandlesLocked;
        public Vector2 Position { get; set; } = position;
        public Vector2 InHandle { get; set; } = new(-20, 0); // Default handle offset
        // Offset from Position
        public Vector2 OutHandle { get; set; } = new(20, 0); // Default handle offset
        // Offset from Position
    }

    public enum HandleType
    {
        None,
        Point,
        InHandle,
        OutHandle
    }

    public class MeshData
    {
        public List<BezierPoint> BezierPoints { get; } = new();
        public List<int> Triangles { get; } = new();
        public int SelectedBezierPointIndex { get; set; } = -1;
        public HandleType SelectedHandleType { get; set; } = HandleType.None;

        public MeshData()
        {
        }
    }
}
