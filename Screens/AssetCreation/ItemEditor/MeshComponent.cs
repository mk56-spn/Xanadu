using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using MemoryPack;
using XanaduProject.Factories;
using XanaduProject.Scenes.ItemEditor;

namespace XanaduProject.Screens.AssetCreation.ItemEditor
{
    [MemoryPackable]
    public partial struct MeshComponent() : IComponent
    {
        public string Name { get; set; } = "New Mesh"; // Added Name property
        public List<BezierPoint> BezierPoints { get; set; } = new(); // Added public setter
       [MemoryPackIgnore]
        public RenderRid RenderRid = RenderRid.Create();
        public Color Color = Colors.White;
        [MemoryPackIgnore]

        public int SelectedBezierPointIndex { get; set; } = -1;
        [MemoryPackIgnore]

        public HandleType SelectedHandleType { get; set; } = HandleType.None;
    }
}
