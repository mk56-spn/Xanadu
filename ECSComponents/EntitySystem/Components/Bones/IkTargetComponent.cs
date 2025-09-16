using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.ECSComponents.EntitySystem.Components.Bones
{
    public struct IkTargetComponent() : IComponent
    {
        public Vector2 TargetPosition { get; set; }
        public bool ElbowUp = true;
        public Entity UpperBoneEntity { get; set; }
        public Entity LowerBoneEntity { get; set; }
    }
}
