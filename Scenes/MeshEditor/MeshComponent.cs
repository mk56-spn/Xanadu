using Friflo.Engine.ECS;
using XanaduProject.Factories;

namespace XanaduProject.Scenes.MeshEditor
{
    public struct MeshComponent : IComponent
    {
        public MeshData MeshData { get; set; }
        public RenderRid RenderRid { get; set; } // NEW: RenderRid for drawing the mesh polygon
    }
}
