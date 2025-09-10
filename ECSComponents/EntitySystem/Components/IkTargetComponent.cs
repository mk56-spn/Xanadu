using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.ECSComponents.EntitySystem.Components
{
    public struct IkTargetComponent() : IComponent
    {
        public Vector2 TargetPosition { get; set; }
        public bool ElbowUp = true;
        public Entity EndEffectorBoneEntity { get; set; }
        public Entity UpperBoneEntity { get; set; }
        public Entity LowerBoneEntity { get; set; }
    }
}
