using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.ECSComponents.EntitySystem.Components
{
    public struct BoneGlobalTransform : IComponent
    {
        public Vector2 GlobalPosition { get; set; }
        public float GlobalAngle { get; set; }
    }
}
