// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.GameDependencies;

namespace XanaduProject.Screens.AssetCreation.Skeleton
{
    public static class SkeletonBuilder
    {
        private static EntityStore entityStore => DiProvider.Get<EntityStore>();

        public static void BuildPose()
        {
            BuildPoseAndGetBoneNames();
        }

        public static List<string> BuildPoseAndGetBoneNames()
        {
            var boneNames = new List<string>();
            var rootEntity = entityStore.CreateEntity(new RootEcs(), new NameEcs("Root"));

            var shoulderEntity = createBoneEntity(entityStore, 20, float.Pi / 2f, rootEntity, "Shoulder");
            boneNames.Add("Shoulder");

            var leftShoulder = createBoneEntity(entityStore, 15, 0, rootEntity);
            leftShoulder.AddTag<RotationLocked>();
            var rightShoulder = createBoneEntity(entityStore, 15, -float.Pi, rootEntity);
            rightShoulder.AddTag<RotationLocked>();;
            shoulderEntity.AddChild(leftShoulder);
            shoulderEntity.AddChild(rightShoulder);

            rootEntity.GetComponent<RootEcs>().MainBone = shoulderEntity;

            var hipEntity = createBoneEntity(entityStore, 50, 0, rootEntity, "Hip");
            boneNames.Add("Hip");

            var leftHip = createBoneEntity(entityStore, 6, 0, rootEntity, "LeftHip");
            boneNames.Add("LeftHip");
            leftHip.AddTag<RotationLocked>();
            var rightHip = createBoneEntity(entityStore, 6, -float.Pi, rootEntity, "RightHip");
            boneNames.Add("RightHip");
            rightHip.AddTag<RotationLocked>();
            hipEntity.AddChild(leftHip);
            hipEntity.AddChild(rightHip);

            rootEntity.AddChild(shoulderEntity);
            shoulderEntity.AddChild(hipEntity);

            // Arms
            createLimb(leftShoulder, rootEntity, new Vector2(-50, 64), true, "LeftArm", boneNames, 35, 30);
            createLimb(rightShoulder, rootEntity,  new Vector2(-40, 70), true, "RightArm", boneNames, 35, 30);

            // Legs
            createLimb(leftHip, rootEntity,new Vector2(0, 140), false, "LeftLeg", boneNames);
            createLimb( rightHip, rootEntity, new Vector2(10, 140), false, "RightLeg", boneNames);
            return boneNames;
        }


        private static void createLimb(Entity parent,Entity root, Vector2 targetPosition, bool bendUpwards, string namePrefix, List<string> boneNames, float lengthTop = 40, float lengthBottom = 35)
        {
            string upperLimbName = $"{namePrefix}Upper";
            var upperLimb = createBoneEntity(entityStore, lengthTop, 0, root, upperLimbName);
            boneNames.Add(upperLimbName);

            string lowerLimbName = $"{namePrefix}Lower";
            var lowerLimb = createBoneEntity(entityStore, lengthBottom, 0, root, lowerLimbName);
            boneNames.Add(lowerLimbName);

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
        private static Entity createBoneEntity(in EntityStore store, float length, float angle = 0, Entity? target = null, string? name = null)
        {
            var boneEntity = store.CreateEntity(new BoneEcs { Length = length, Angle = angle }, new BoneGlobalTransform { Target = target ?? default });

            if (name != null)
                boneEntity.AddComponent(new NameEcs(name));

            return boneEntity;
        }
    }
}
