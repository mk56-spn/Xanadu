using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System.Collections.Generic;
using XanaduProject.ECSComponents.EntitySystem.BoneSystems;
using XanaduProject.ECSComponents.EntitySystem.Components;

namespace XanaduProject.Scenes
{
    public class IkRiggingEcsManager
    {
        public readonly EntityStore EntityStore;
        public readonly SystemRoot SystemRoot;

        public IkRiggingEcsManager()
        {
            EntityStore = new EntityStore();
            SystemRoot = new SystemRoot();
            SystemRoot.Add(new BoneTransformSystem(EntityStore));
            SystemRoot.Add(new IkSolverSystem(EntityStore));
            SystemRoot.Add(new BoneRenderingSystem());
            SystemRoot.AddStore(EntityStore);
        }

        public void SetupInitialScene(List<Entity> allBones, List<Entity> ikTargetEntities)
        {
            // Spine
            var shoulderEntity = EntityStore.CreateEntity(
                new BoneEcs { Length = 20, Angle = float.Pi / 4 * 2.5f },
                new BoneGlobalTransform { GlobalPosition = new Vector2() }
            );
            allBones.Add(shoulderEntity);

            var hipEntity = EntityStore.CreateEntity(new BoneEcs { ParentEntity = shoulderEntity, Length = 50 });
            allBones.Add(hipEntity);

            // Arms
            CreateLimb(shoulderEntity,
                new Vector2(-50, 64) + shoulderEntity.GetComponent<BoneGlobalTransform>().GlobalPosition, true, 35, 30, allBones, ikTargetEntities);
            CreateLimb(shoulderEntity,
                new Vector2(-40, 70) + shoulderEntity.GetComponent<BoneGlobalTransform>().GlobalPosition, true, 35, 30, allBones, ikTargetEntities);

            // Legs
            var hipInitialPos = shoulderEntity.GetComponent<BoneGlobalTransform>().GlobalPosition + new Vector2(0, 50);
            CreateLimb(hipEntity, new Vector2(0, 120) + hipInitialPos, false, 40, 35, allBones, ikTargetEntities);
            CreateLimb(hipEntity, new Vector2(10, 130) + hipInitialPos, false, 40, 35, allBones, ikTargetEntities);
        }

        private void CreateLimb(Entity parent, Vector2 targetPosition, bool bendUpwards, float lengthTop,
            float lengthBottom, List<Entity> allBones, List<Entity> ikTargetEntities)
        {
            var upperLimb = EntityStore.CreateEntity(new BoneEcs { Length = lengthTop, ParentEntity = parent });
            var lowerLimb = EntityStore.CreateEntity(
                new BoneEcs { ParentEntity = upperLimb, Length = lengthBottom },
                new BoneGlobalTransform());
            allBones.Add(upperLimb);
            allBones.Add(lowerLimb);

            var ikTargetEntity = EntityStore.CreateEntity(new IkTargetComponent
            {
                UpperBoneEntity = upperLimb,
                LowerBoneEntity = lowerLimb,
                ElbowUp = bendUpwards,
                TargetPosition = targetPosition,
            });

            ikTargetEntities.Add(ikTargetEntity);
        }

        public ArchetypeQuery<IkTargetComponent> GetIkTargetsQuery()
        {
            return EntityStore.Query<IkTargetComponent>();
        }

        public ArchetypeQuery<BoneEcs, BoneGlobalTransform> GetBonesQuery()
        {
            return EntityStore.Query<BoneEcs, BoneGlobalTransform>();
        }

        public void Update()
        {
            SystemRoot.Update(default);
        }
    }
}
