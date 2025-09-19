using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;

namespace XanaduProject.ECSComponents.EntitySystem.BoneSystems
{
    public class IkSolverSystem : QuerySystem<IkTargetComponent>
    {
        protected override void OnUpdate()
        {
            Query.ForEachEntity((ref IkTargetComponent ik, Entity _) =>
            {
                var upperBoneEntity = ik.UpperBoneEntity;
                var lowerBoneEntity = ik.LowerBoneEntity;


                ref var upperBone = ref upperBoneEntity.GetComponent<BoneEcs>();
                ref var lowerBone = ref lowerBoneEntity.GetComponent<BoneEcs>();
                float l1 = upperBone.Length;
                float l2 = lowerBone.Length;

                var parentEntity = upperBoneEntity.Parent;


                ref var parentTransform = ref parentEntity.GetComponent<BoneGlobalTransform>();
                var startPosition = parentTransform.GlobalPosition;
                float parentAngle = parentTransform.GlobalAngle;

                var targetPosition = ik.TargetPosition;
                var direction = targetPosition - startPosition;
                float dist = direction.Length();

                if (dist > l1 + l2)
                {
                    // Target is out of reach
                    float angle = direction.Angle();
                    upperBone.Angle = angle - parentAngle;
                    lowerBone.Angle = 0;
                }
                else
                {
                    // Target is in reach
                    float angleBase = direction.Angle();

                    // Using Law of Cosines to find the angles of the triangle
                    // Clamp dist to avoid NaN from Acos due to floating point inaccuracies
                    float distClamped = Math.Max(0, Math.Min(l1 + l2, dist));

                    float angle1 = (float)Math.Acos((l1 * l1 + distClamped * distClamped - l2 * l2) / (2 * l1 * distClamped));
                    float angle2 = (float)Math.Acos((l1 * l1 + l2 * l2 - distClamped * distClamped) / (2 * l1 * l2));

                    float upperBoneGlobalAngle = angleBase + (ik.ElbowUp ? -angle1 : angle1);
                    upperBone.Angle = upperBoneGlobalAngle - parentAngle;
                    lowerBone.Angle = (ik.ElbowUp ? 1 : -1) * ((float)Math.PI - angle2);
                }
            });
        }
    }
}
