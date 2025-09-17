using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XanaduProject.Scenes.MeshEditor
{
    public record MeshData
    {
        public string Name { get; set; } = "New Mesh"; // Added Name property

        public List<BezierPoint> BezierPoints { get; set; } = new(); // Added public setter

        [JsonIgnore]
        public List<int> Triangles { get; } = new();
        [JsonIgnore]
        public int SelectedBezierPointIndex { get; set; } = -1;

        [JsonIgnore]
        public HandleType SelectedHandleType { get; set; } = HandleType.None;
    }
}
