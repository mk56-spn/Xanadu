using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.Animation.Arrays;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
using XanaduProject.IO.Indexes;
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

        public PoseAnimatingScreen(string? path = null)
        {
            CloseTargetScreen = new AssetCreationScreen();
            AnimationInfo animationInfo;

            if (path != null)
            {
                AnimationPath = path;
                var loadAnimation = AnimationIo.LoadAnimation(path);
                animationInfo = new AnimationInfo { Duration = loadAnimation.Duration };
                PoseUtils.CreateTracksForPose(loadAnimation);
            }
            else
            {
                animationInfo = new AnimationInfo { Duration = 1};
                PoseUtils.CreateTracksForPose();
            }
            Store.CreateEntity(animationInfo, new UniqueEntity(animation));
            poseAnimatingLayout = new PoseAnimatingLayout();
            AddChild(poseAnimatingLayout);
        }

        protected override void PostBaseSystems(SystemRoot root)
        {
            root.Add(new PoseAnimationSystem());
            root.Add(new LoopingSystem());
            root.Add(new InitializeVisuals(PoseServices.GetViewer(), SkinIndex.GetAllSkins().First().Value));

            root.Add(new PoseAnimationUiBuilderSystem(poseAnimatingLayout));
            root.Add(new KeyFrameEditSystem<Vector2,VectorArrayEcs>());
            root.Add(new KeyFrameEditSystem<float,AngleArrayEcs>());
        }
    }
}
