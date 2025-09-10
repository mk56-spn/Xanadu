using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.ECSComponents.EntitySystem.Components
{
    public struct BoneEcs() : IComponent
    {
        public float Length = 40; // was: readonly; allow setting per-bone to get proper proportions
        public float Angle = 0;
        public Entity ParentEntity { get; set; } // Reference to the parent bone entity
    }
}
