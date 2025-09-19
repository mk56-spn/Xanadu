    using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Factories;

namespace XanaduProject.ECSComponents.EntitySystem.Components
{
    public struct RenderRidComponent(RenderRid renderRid) : IComponent
    {
        public RenderRid RenderRid = renderRid;
    }
}
