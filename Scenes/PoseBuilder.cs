// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.GameDependencies;

namespace XanaduProject.Scenes
{
    public static class PoseBuilder
    {
        private static EntityStore entityStore => DiProvider.Get<EntityStore>();

        public static void BuildPose()
        {
            var rootEntity = entityStore.CreateEntity(new RootEcs());
            var shoulderEntity = entityStore.CreateEntity(new BoneEcs
            {
                Length = 20,
                // Place upright
                Angle = float.Pi / 2f,
            }, new BoneGlobalTransform { Target = rootEntity });

            var leftShoulder = entityStore.CreateEntity(new BoneEcs { Length = 15, Angle = 0}, new BoneGlobalTransform { Target = rootEntity }, Tags.Get<RotationLocked>());
            var rightShoulder = entityStore.CreateEntity(new BoneEcs { Length = 15, Angle = -float.Pi }, new BoneGlobalTransform { Target = rootEntity }, Tags.Get<RotationLocked>() );
            shoulderEntity.AddChild(leftShoulder);
            shoulderEntity.AddChild(rightShoulder);

            rootEntity.GetComponent<RootEcs>().MainBone = shoulderEntity;

            var hipEntity = entityStore.CreateEntity(new BoneEcs { Length  = 50, }, new BoneGlobalTransform { Target = rootEntity });

            var leftHip = entityStore.CreateEntity(new BoneEcs { Length = 6, Angle = 0}, new BoneGlobalTransform { Target = rootEntity }, Tags.Get<RotationLocked>());
            var rightHip = entityStore.CreateEntity(new BoneEcs { Length = 6, Angle = -float.Pi }, new BoneGlobalTransform { Target = rootEntity }, Tags.Get<RotationLocked>());
            hipEntity.AddChild(leftHip);
            hipEntity.AddChild(rightHip);

            rootEntity.AddChild(shoulderEntity);
            shoulderEntity.AddChild(hipEntity);

            // Arms
            createLimb(leftShoulder, rootEntity, new Vector2(-50, 64), true, 35, 30);
            createLimb(rightShoulder, rootEntity,  new Vector2(-40, 70), true, 35, 30);

            // Legs
            createLimb(leftHip, rootEntity,new Vector2(0, 120), false);
            createLimb( rightHip, rootEntity, new Vector2(10, 130), false);
        }


        private static void createLimb(Entity parent,Entity root, Vector2 targetPosition, bool bendUpwards, float lengthTop = 40, float lengthBottom = 35)
        {

            var upperLimb = entityStore.CreateEntity(new BoneGlobalTransform { Target = root },
                new BoneEcs { Length = lengthTop});
            var lowerLimb = entityStore.CreateEntity(new BoneGlobalTransform { Target = root },
                new BoneEcs { Length = lengthBottom });

            parent.AddChild(upperLimb);
            upperLimb.AddChild(lowerLimb);
            entityStore.CreateEntity(new IkTargetComponent
            {
                UpperBoneEntity = upperLimb,
                LowerBoneEntity = lowerLimb,
                ElbowUp = bendUpwards,
                TargetPosition = targetPosition,
            });
        }
    }
}
