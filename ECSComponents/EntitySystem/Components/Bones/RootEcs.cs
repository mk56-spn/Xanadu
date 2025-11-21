using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Factories;

namespace XanaduProject.ECSComponents.EntitySystem.Components.Bones
{
    [ComponentKey(null)]
    public struct RootEcs() : IComponent
    {
        public RenderRid Canvas = RenderRid.Create().SetZIndex(10);
        public Entity MainBone;
        public Vector2 Position;
    }
}
