using System.Collections.Generic;
using System.Text.Json.Serialization;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Factories;

namespace XanaduProject.Scenes.ItemEditor
{
    public struct MeshComponent() : IComponent
    {
        public string Name { get; set; } = "New Mesh"; // Added Name property
        public List<BezierPoint> BezierPoints { get; set; } = new(); // Added public setter
        public RenderRid RenderRid = RenderRid.Create();

        public Color Color = Colors.White;

        [JsonIgnore]
        public int SelectedBezierPointIndex { get; set; } = -1;

        [JsonIgnore]
        public HandleType SelectedHandleType { get; set; } = HandleType.None;
    }
}
