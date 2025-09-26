using System.Linq;
using Godot;
using XanaduProject.IO.Indexes;
using XanaduProject.Screens.AssetCreation.BoneMapping;
using XanaduProject.Screens.AssetCreation.Skeleton;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating
{
    public partial class PoseAnimatingScreen : PoseScreen
    {
        private VBoxContainer container = new();
        private CenterContainer centerContainer = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        private Control center = new();
        public PoseAnimatingScreen()
        {
            Visible = true;
            AddChild(container);
            container.AddChild(centerContainer);
            centerContainer.AddChild(center);


            Root.Add(new ItemDisplaySystem(center, SkinIndex.GetAllSkins().First().Value));
            Root.Add(new PoseAnimationSystem());

            Root.Add(new PoseAnimationUiBuilderSystem(container));
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            center.Rotation = float.Pi / 2;
        }

        public override void _Ready()
        {

            container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
    }
}
