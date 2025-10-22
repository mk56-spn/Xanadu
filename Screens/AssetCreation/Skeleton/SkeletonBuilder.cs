// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;

namespace XanaduProject.Screens.AssetCreation.Skeleton
{
    public static class SkeletonBuilder
    {
        private static EntityStore entityStore => GameServices.Store;

        public static void BuildPose()
        {
            BuildPoseAndGetBoneNames();
        }

        public static List<string> GetBoneNames()
        {
            var boneNames = new List<string>
            {
                "Head",
                "Hip",
                "LeftHip",
                "RightHip"
            };

            // Arms
            addLimbNames("LeftArm", boneNames);
            addLimbNames("RightArm", boneNames);

            // Legs
            addLimbNames("LeftLeg", boneNames);
            addLimbNames("RightLeg", boneNames);
            return boneNames;
        }

        private static void addLimbNames(string namePrefix, List<string> boneNames)
        {
            boneNames.Add($"{namePrefix}Upper");
            boneNames.Add($"{namePrefix}Lower");
        }

        public static void BuildPoseAndGetBoneNames()
        {
            var boneNames = new List<string>();
            var rootEntity = entityStore.CreateEntity(new RootEcs(), new NameEcs("Root"));

            var shoulderEntity = createBoneEntity(entityStore, 20, float.Pi / 2f, rootEntity, "Head");
            boneNames.Add("Head");

            var leftShoulder = createBoneEntity(entityStore, 15, 0, rootEntity);
            leftShoulder.AddTag<RotationLocked>();
            leftShoulder.AddComponent(new DepthEcs(1));
            var rightShoulder = createBoneEntity(entityStore, 15, -float.Pi, rootEntity);
            rightShoulder.AddTag<RotationLocked>();;
            rightShoulder.AddComponent(new DepthEcs(-1));
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
        }


        private static void createLimb(Entity parent,Entity root, Vector2 targetPosition, bool bendUpwards, string namePrefix, List<string> boneNames, float lengthTop = 40, float lengthBottom = 35)
        {

            string upperLimbName = $"{namePrefix}Upper";
            var upperLimb = createBoneEntity(entityStore, lengthTop, 0, root, upperLimbName);
            boneNames.Add(upperLimbName);
            upperLimb.AddTag<IkControlled>();

            string lowerLimbName = $"{namePrefix}Lower";
            var lowerLimb = createBoneEntity(entityStore, lengthBottom, 0, root, lowerLimbName);
            boneNames.Add(lowerLimbName);
            lowerLimb.AddTag<IkControlled>();

            parent.AddChild(upperLimb);
            upperLimb.AddChild(lowerLimb);
            entityStore.CreateEntity(
                new IkTargetComponent
                {
                    UpperBoneEntity = upperLimb,
                    LowerBoneEntity = lowerLimb,
                    ElbowUp = bendUpwards,
                    TargetPosition = targetPosition,
                },
                new NameEcs($"{namePrefix}Target")
            );
        }
        private static Entity createBoneEntity(in EntityStore store, float length, float angle = 0, Entity? target = null, string? name = null, int depth = 0)
        {
            var boneEntity = store.CreateEntity(new BoneEcs { Length = length, Angle = angle }, new BoneGlobalTransform { Target = target ?? default });

            if (name != null)
                boneEntity.AddComponent(new NameEcs(name));

            return boneEntity;
        }
    }
}
