using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using XanaduProject.ECSComponents.Animation;
using XanaduProject.ECSComponents.Animation.Arrays;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
using XanaduProject.Screens.AssetCreation.BoneMapping;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Systems;
using XanaduProject.Stage.Masters.Composer;
using XanaduProject.Utils;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating
{
    public partial class PoseAnimatingScreen : PoseScreen
    {
        private static readonly string animation = "Animation";
        public static ref AnimationInfo Info => ref GameServices.Store.GetUniqueEntity(animation).GetComponent<AnimationInfo>();
        private PoseAnimatingLayout poseAnimatingLayout;
        public string? AnimationPath { get; set; }

        public PoseAnimatingScreen(string path)
        {
            AnimationPath = path;
            var v = AnimationIo.LoadAnimation(GameServices.Store, path);
            Store.CreateEntity(v, new UniqueEntity(animation));

            Store.CreateEntity();
            poseAnimatingLayout = new PoseAnimatingLayout();
            AddChild(poseAnimatingLayout);
        }

        public PoseAnimatingScreen()
        {
            AnimationPath = null;
            Store.CreateEntity(new AnimationInfo { Duration = 1, AnimationPos = 0 }, new UniqueEntity(animation));
            poseAnimatingLayout = new PoseAnimatingLayout();
            AddChild(poseAnimatingLayout);
            PoseUtils.CreateTracksForPose();
        }

        protected override void PostBaseSystems(SystemRoot root)
        {
            root.Add(new PoseAnimationSystem());
            root.Add(new ItemDisplaySystem());
            root.Add(new LoopingSystem());
            root.Add(new PoseAnimationUiBuilderSystem(poseAnimatingLayout));
            root.Add(new KeyFrameEditSystem<Vector2,VectorArrayEcs>());
            root.Add(new KeyFrameEditSystem<float,AngleArrayEcs>());
        }
    }
}
