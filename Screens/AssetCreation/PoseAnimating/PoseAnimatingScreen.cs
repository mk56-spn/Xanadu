using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.BoneMapping;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Systems;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating
{
    public partial class PoseAnimatingScreen : PoseScreen
    {
        public static readonly string ANIMATION = "Animation";
        public static ref AnimationInfo Info => ref GameServices.Store.GetUniqueEntity(ANIMATION).GetComponent<AnimationInfo>();
        private PoseAnimatingLayout poseAnimatingLayout;
        public PoseAnimatingScreen()
        {
            Store.CreateEntity(new AnimationInfo { Duration = 1, AnimationPos = 0 }, new UniqueEntity(ANIMATION));
            poseAnimatingLayout = new PoseAnimatingLayout();
            AddChild(poseAnimatingLayout);
        }

        protected override void PostBaseSystems(SystemRoot root)
        {
            root.Add(new PoseAnimationSystem());
            root.Add(new ItemDisplaySystem());
            root.Add(new LoopingSystem());
            root.Add(new PoseAnimationUiBuilderSystem(poseAnimatingLayout));
        }
    }
}
