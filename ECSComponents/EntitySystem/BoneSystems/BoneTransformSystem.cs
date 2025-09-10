using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components;

namespace XanaduProject.ECSComponents.EntitySystem.BoneSystems
{
    public class BoneTransformSystem(EntityStore entityStore) : QuerySystem
    {
        protected override void OnUpdate()
        {
            var processedTransforms = new Dictionary<int, BoneGlobalTransform>();

            entityStore.Query<BoneEcs>().ForEachEntity((ref BoneEcs bone, Entity entity) =>
            {
                if (!processedTransforms.ContainsKey(entity.Id))
                {
                    calculateAndCacheGlobalTransform(entity.Id, ref bone, processedTransforms);
                }
            });

            CommandBuffer.Playback();
        }

       private BoneGlobalTransform calculateAndCacheGlobalTransform(int entityId, ref BoneEcs bone, Dictionary<int, BoneGlobalTransform> cache)
        {
            if (cache.TryGetValue(entityId, out var globalTransform))
            {
                return globalTransform;
            }

            Vector2 parentGlobalPosition = Vector2.Zero;
            float parentGlobalAngle = 0f;

            if (!bone.ParentEntity.IsNull)
            {
                if (entityStore.TryGetEntityById(bone.ParentEntity.Id, out var parentEntity))
                {
                    if (parentEntity.TryGetComponent(out BoneEcs parentBone))
                    {
                        var parentGlobal = calculateAndCacheGlobalTransform(parentEntity.Id, ref parentBone, cache);
                        parentGlobalPosition = parentGlobal.GlobalPosition;
                        parentGlobalAngle = parentGlobal.GlobalAngle;

                        // Child starts at the END of the parent bone: advance by PARENT length, not current bone length
                        // This makes the transform consistent with the renderer which uses the child's length to draw its endpoint.
                        var parentEndOffset = new Vector2(
                            Mathf.Cos(parentGlobalAngle),
                            Mathf.Sin(parentGlobalAngle)
                        ) * parentBone.Length;

                        parentGlobalPosition += parentEndOffset;
                    }
                }
            }

            // Calculate current bone's global angle
            float currentGlobalAngle = parentGlobalAngle + bone.Angle;

            // For root bones (no parent), their start is at the origin (or another configured root position)
            Vector2 currentGlobalPosition = bone.ParentEntity.IsNull
                ? Vector2.Zero
                : parentGlobalPosition;

            globalTransform = new BoneGlobalTransform
            {
                GlobalPosition = currentGlobalPosition,
                GlobalAngle = currentGlobalAngle
            };

            Entity entity = entityStore.GetEntityById(entityId);
            CommandBuffer.AddComponent(entity.Id, globalTransform);
            cache[entityId] = globalTransform;
            return globalTransform;
        }
    }
}
