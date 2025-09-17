using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Factories;

namespace XanaduProject.Scenes.MeshEditor
{
    public struct MeshComponent() : IComponent
    {
        public MeshData MeshData;
        public RenderRid RenderRid = RenderRid.Create();
        public Color Color = Colors.White;
    }
}
