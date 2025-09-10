using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.GameDependencies;

namespace XanaduProject.ECSComponents.EntitySystem.BoneSystems
{
    public class IkSolverSystem(EntityStore entityStore) : QuerySystem<IkTargetComponent>
    {
        protected override void OnUpdate()
        {
            Query.ForEachEntity((ref IkTargetComponent ikTarget, Entity _) =>
            {
                Entity upperBoneEntity = ikTarget.UpperBoneEntity;
                Entity lowerBoneEntity = ikTarget.LowerBoneEntity;

                if (!upperBoneEntity.TryGetComponent(out BoneEcs upperBone) ||
                    !lowerBoneEntity.TryGetComponent(out BoneEcs lowerBone) ||
                    !upperBoneEntity.TryGetComponent(out BoneGlobalTransform upperBoneGlobalTransform)) return;

                // Get the start position of the upper bone (which is its global position)
                Vector2 p1 = upperBoneGlobalTransform.GlobalPosition;
                float l1 = upperBone.Length;
                float l2 = lowerBone.Length;
                Vector2 targetPosition = ikTarget.TargetPosition;

                // Distance from p1 to target
                float d = p1.DistanceTo(targetPosition);

                // Declare targetAngleFromP1 once
                float targetAngleFromP1;

                // Check reachability
                if (d > l1 + l2)
                {
                    // Target is out of reach, extend fully towards target
                    targetAngleFromP1 = p1.AngleToPoint(targetPosition);
                    upperBone.Angle = targetAngleFromP1 - upperBoneGlobalTransform.GlobalAngle; // Convert to relative
                    lowerBone.Angle = 0; // Straighten out
                }
                else if (d < Mathf.Abs(l1 - l2))
                {
                    // Target is too close, collapse fully towards target
                    targetAngleFromP1 = p1.AngleToPoint(targetPosition);
                    upperBone.Angle = targetAngleFromP1 - upperBoneGlobalTransform.GlobalAngle; // Convert to relative
                    lowerBone.Angle = Mathf.Pi; // Bend back on itself
                }
                else
                {
                    // Law of Cosines to find angles
                    float cosAlpha = (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d);
                    float cosBeta = (l1 * l1 + l2 * l2 - d * d) / (2 * l1 * l2);

                    // Clamp values to avoid NaN from Acos due to floating point inaccuracies
                    float alpha = Mathf.Acos(Mathf.Clamp(cosAlpha, -1.0f, 1.0f));
                    float beta = Mathf.Acos(Mathf.Clamp(cosBeta, -1.0f, 1.0f));

                    // Angle from p1 to target
                    targetAngleFromP1 = p1.AngleToPoint(targetPosition);

                    // Calculate the global angle for bone1
                    float globalAngleUpperBone;
                    if (ikTarget.ElbowUp)
                    {
                        globalAngleUpperBone = targetAngleFromP1 + alpha;
                    }
                    else
                    {
                        globalAngleUpperBone = targetAngleFromP1 - alpha;
                    }

                    // Convert globalAngleUpperBone to upperBone.Angle (relative to parent)
                    // We need the parent's global angle. This implies the BoneTransformSystem must run BEFORE IkSolverSystem.
                    // For now, let's assume upperBoneGlobalTransform.GlobalAngle is the parent's global angle if upperBone has a parent.
                    // If upperBone is a root bone, its parent's global angle is 0.
                    float parentGlobalAngle = 0f;
                    if (!upperBone.ParentEntity.IsNull && entityStore.TryGetEntityById(upperBone.ParentEntity.Id, out var parentEnt) && parentEnt.TryGetComponent(out BoneGlobalTransform parentGlobalTrans))
                    {
                        parentGlobalAngle = parentGlobalTrans.GlobalAngle;
                    }
                    upperBone.Angle = globalAngleUpperBone - parentGlobalAngle;

                    // Calculate the relative angle for lowerBone
                    // The calculation for globalAngleLowerBone needs to be relative to upperBone's *new* global angle
                    // after upperBone.Angle has been set. So we re-calculate upperBone's global angle here.
                    // This is a bit tricky because BoneTransformSystem runs once per frame.
                    // For now, we'll use the newly calculated globalAngleUpperBone.
                    float upperBoneNewGlobalAngle = globalAngleUpperBone; // This is the global angle we just calculated for the upper bone

                    float globalAngleLowerBone;
                    if (ikTarget.ElbowUp)
                    {
                        globalAngleLowerBone = upperBoneNewGlobalAngle - (Mathf.Pi - beta);
                    }
                    else
                    {
                        globalAngleLowerBone = upperBoneNewGlobalAngle + (Mathf.Pi - beta);
                    }

                    lowerBone.Angle = globalAngleLowerBone - upperBoneNewGlobalAngle;
                }
                CommandBuffer.AddComponent(upperBoneEntity.Id, upperBone);
                CommandBuffer.AddComponent(lowerBoneEntity.Id, lowerBone);;
            });
        }
    }
}
