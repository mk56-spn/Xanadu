using System.Collections.Generic;
using Godot;

namespace XanaduProject.Scenes.MeshEditor
{
    public struct BezierPoint
    {
        public Vector2 Position { get; set; }
        public Vector2 InHandle { get; set; } // Offset from Position
        public Vector2 OutHandle { get; set; } // Offset from Position

        public BezierPoint(Vector2 position)
        {
            Position = position;
            InHandle = new Vector2(-20, 0); // Default handle offset
            OutHandle = new Vector2(20, 0); // Default handle offset
        }
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
