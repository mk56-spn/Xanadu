
using Godot;
using XanaduProject.Utils;

namespace XanaduProject.Screens.AssetCreation.BoneMapping
{
    public partial class ItemBoneMappingScreenUi : Control
    {
        public ItemList BoneList { get; } = new();
        public Button AddItemButton { get; } = new() { Text = "Add/Edit Item" };
        public Button RemoveItemButton { get; } = new() { Text = "Remove Item" };
        public Button SaveSkinButton { get; } = new() { Text = "Save Skin" };
        public HBoxContainer MainHBox { get; } = new();
        public VBoxContainer BoneListVBox { get; } = new();

        public ViewerPanelContainer ViewerPanel { get; } = new();
        public ItemBoneMappingScreenUi()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var marginContainer = new MarginContainer();
            marginContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(marginContainer);

            MainHBox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            marginContainer.AddChild(MainHBox);

            setupBoneList();
            setupButtons();
            setupViewer();
        }

        private void setupBoneList()
        {
            BoneListVBox.SetHSizeFlags(SizeFlags.ExpandFill);
            MainHBox.AddChild(BoneListVBox);

            var title = new Label { Text = "Bone -> Item Mapping" };
            BoneListVBox.AddChild(title);

            BoneListVBox.AddChild(BoneList);
            BoneList.SetVSizeFlags(SizeFlags.ExpandFill);
        }

        private void setupButtons()
        {
            var hBoxButtons = new HBoxContainer();
            hBoxButtons.AddChildren([ AddItemButton, RemoveItemButton, SaveSkinButton]);
            BoneListVBox.AddChild(hBoxButtons);
        }

        private void setupViewer()
        {
            MainHBox.AddChild(ViewerPanel);
        }

        public partial class ViewerPanelContainer : PanelContainer
        {

            public readonly Control  ViewerCentre = new();
            public ViewerPanelContainer()
            {
                ClipContents = true;

                CustomMinimumSize = new Vector2(500, 0);
                SetHSizeFlags(SizeFlags.ShrinkEnd);
                SetVSizeFlags(SizeFlags.ExpandFill);
                AddThemeColorOverride("panel_color", new Color(0.15f, 0.15f, 0.2f));

                AddChild(ViewerCentre);
            }

            public override void _Process(double delta)
            {
                base._Process(delta);
                ViewerCentre.Scale = new Vector2(3, 3);
                ViewerCentre.Position  = Size / 2;
            }
        }
    }
}
