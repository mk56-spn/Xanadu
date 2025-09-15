using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.ECSComponents.EntitySystem.Components.Bones
{
    public struct BoneGlobalTransform : ILinkComponent
    {
        public Vector2 GlobalPosition { get; set; }
        public float GlobalAngle { get; set; }

        public Entity Target;
        public Entity GetIndexedValue() => Target;
    }
}
