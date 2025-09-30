using System;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Composer;
using XanaduProject.IO.Indexes;
using XanaduProject.Screens.AssetCreation.BoneMapping;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;
using XanaduProject.Screens.AssetCreation.Skeleton;
using XanaduProject.Stage.Masters.Composer.TrackVisualiser;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating
{
    public partial class PoseAnimatingScreen : PoseScreen
    {
        public static readonly string ANIMATION = "Animation";
        private VBoxContainer container = new();
        private CenterContainer centerContainer = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };

        private Control center = new();
        private HSlider animationSlider =
            new()
            {
                MinValue = 0,
                MaxValue = 1,
                Step = 0.01f,
                SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            };


        private readonly PoseAnimationSystem poseAnimationSystem;
        public PoseAnimatingScreen()
        {


            Store.CreateEntity(new UniqueEntity(ANIMATION),new AnimationInfo()
            {
                Duration = 1
            });
            Visible = true;
            AddChild(container);
            topBar();
            container.AddChild(centerContainer);
            centerContainer.AddChild(center);
            var skinInfo = SkinIndex.GetAllSkins().First();


            Root.Add(new ItemDisplaySystem(center, skinInfo.Value));

            Root.Add(poseAnimationSystem = new PoseAnimationSystem());

            center.AddChild(new BoneInputHandler(Store) { });
            bottomBar();
        }

        private void bottomBar()
        {
            ref var v = ref Store.Query<AnimationInfo>().Entities.Single().GetComponent<AnimationInfo>();

            animationSlider.CustomMinimumSize = new Vector2(AnimationTracksManager.SPACING * v.Duration, 20);

            animationSlider.ValueChanged += value =>
            {
                ref var v = ref Store.Query<AnimationInfo>().Entities.Single().GetComponent<AnimationInfo>();

                v.AnimationPos = (float)value;
            };
            container.AddChild(animationSlider);

            Root.Add(new PoseAnimationUiBuilderSystem(container));
        }

        private void topBar()
        {
            HBoxContainer barContainer = new HBoxContainer()
            {
                CustomMinimumSize = new Vector2(0,50)
            };

            CheckButton button;
            barContainer.AddChild(button = new CheckButton()
            {
                CustomMinimumSize = new Vector2(50,50)
            });
            button.Toggled += on =>
            {
                poseAnimationSystem.Enabled = on;
            };
            container.AddChild(barContainer);
        }


        public override void _Process(double delta)
        {
            base._Process(delta);

            if (poseAnimationSystem.Enabled)
            {
                ref var animationInfo = ref Store.Query<AnimationInfo>().Entities.Single().GetComponent<AnimationInfo>();
                animationSlider.Value = (animationSlider.Value + delta / animationInfo.Duration) % 1.0d;
            }
        }

        public override void _Ready()
        {
            container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
    }
}
