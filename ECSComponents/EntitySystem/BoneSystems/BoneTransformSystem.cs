using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;

namespace XanaduProject.ECSComponents.EntitySystem.BoneSystems
{
    public class BoneTransformSystem : QuerySystem<RootEcs>
    {
        protected override void OnUpdate()
        {
            // Parallelised query on 10 cores
            var v = Query.ForEach((components, entities) =>
            {
                for (int n = 0; n < entities.Length; n++)
                {
                    updateBoneTransformsRecursive(components[n].MainBone, components[n].Position, 0f);
                }
            });
            v.RunParallel();
        }

        private void updateBoneTransformsRecursive(Entity boneEntity, Vector2 parentEndPosition,
            float parentGlobalAngle)
        {
            ref var boneEcs = ref boneEntity.GetComponent<BoneEcs>();
            // Calculate the bone's global angle.
            float currentGlobalAngle;
            if (boneEntity.Tags.Has<RotationLocked>())
            {
                currentGlobalAngle = boneEcs.Angle;
            }
            else
            {
                currentGlobalAngle = parentGlobalAngle + boneEcs.Angle;
            }

            ref var v = ref boneEntity.GetComponent<BoneGlobalTransform>();
            v.GlobalPosition = parentEndPosition;
            v.GlobalAngle = currentGlobalAngle;

            // Calculate the end position of the current bone, which is the start position for its children.
            var rotation = Vector2.Right.Rotated(currentGlobalAngle);
            Vector2 currentEndPosition = parentEndPosition + rotation * boneEcs.Length;

            foreach (var childEntity in boneEntity.ChildEntities)
                updateBoneTransformsRecursive(childEntity, currentEndPosition, currentGlobalAngle);
        }
    }
}
