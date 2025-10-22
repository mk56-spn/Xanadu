
using System.Collections.Generic;
using System.Linq;
using Godot;
using Xanadu.Singletons;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
using XanaduProject.IO.Indexes;
using XanaduProject.Screens.ScreenStructure;
using XanaduProject.Singleton;
using XanaduProject.UiElements;
using XanaduProject.Utils;
using AnimatedListControl = XanaduProject.UiElements.AnimatedListControl;

namespace XanaduProject.Screens.AssetCreation.BoneMapping
{
    public partial class BoneMappingLayout : Control
    {
        private readonly MainScreen mainScreen;
        private AnimatedHoverButton addItemButton { get; } = new("Add/Edit Item", fontSize: 20);
        private AnimatedHoverButton removeItemButton { get; } = new("Remove Item", fontSize: 20);
        private AnimatedHoverButton saveSkinButton { get; } = new("Save Skin", fontSize: 20);
        private AnimatedHoverButton closeEditorButton { get; } = new("Close Editor") { Visible = false };
        private HBoxContainer mainHBox { get; } = new();
        private VBoxContainer boneListVBox { get; } = new();
        private readonly ItemEditor.ItemEditor activeItemEditor = new()
        {
        };

        public ViewerPanelContainer ViewerPanel { get; } = new();
        public BoneMappingLayout(MainScreen mainScreen)
        {
            this.mainScreen = mainScreen;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var marginContainer = new MarginContainer();
            marginContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(marginContainer);

            mainHBox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            marginContainer.AddChild(mainHBox);

            setupBoneList();
            setupButtons();
            setupViewer();


            AnimatedListControl animatedListControl = new AnimatedListControl() { ZIndex = 3, SizeFlagsHorizontal = SizeFlags.ShrinkBegin};
            foreach (string boneName in ItemBoneMappingScreen.Info.Skin.BoneNames)
            {
                animatedListControl.AddItem(boneName, () => ItemBoneMappingScreen.Info.SelectedBone = boneName, 13);
            }
            boneListVBox.AddChild(animatedListControl);
            ViewerPanel.AddOverlay(activeItemEditor);
        }

        private void setupBoneList()
        {
            boneListVBox.SetHSizeFlags(SizeFlags.ShrinkBegin);
            mainHBox.AddChild(boneListVBox);

            var title = new Label { Text = "Bone -> Item Mapping" };


            boneListVBox.AddChild(title);

        }

        private void setupButtons()
        {
            var hBoxButtons = new VBoxContainer();
            hBoxButtons.AddChildren([ addItemButton, removeItemButton, saveSkinButton, closeEditorButton]);
            boneListVBox.AddChild(hBoxButtons);

            addItemButton.Pressed += OnAddItemPressed;
            closeEditorButton.Pressed += OnCloseEditorPressed;
            saveSkinButton.Pressed += () => StandardPoseSkinIo.Save(ItemBoneMappingScreen.Info.Skin);
        }

        private void setupViewer()
        {
            mainHBox.AddChild(ViewerPanel);
        }


        public partial class ViewerPanelContainer : PanelContainer
        {
            public readonly Control ViewerCentre = new();
            private readonly Control editorOverlay = new();

            public void AddOverlay(Control control)
            {
                editorOverlay.AddChild(control);
            }
            public ViewerPanelContainer()
            {
                ClipContents = true;


                SetHSizeFlags(SizeFlags.ExpandFill);
                SetVSizeFlags(SizeFlags.ExpandFill);

                AddChild(ViewerCentre);
                AddChild(editorOverlay);

                editorOverlay.Modulate = new Color(1, 1, 1, 0.7f); // 70% opacity
            }

            public override void _Process(double delta)
            {
                base._Process(delta);


                ViewerCentre.Scale = new Vector2(3, 3);
                ViewerCentre.Position = Size / 2;

                QueueRedraw();
            }
        }


        private void OnAddItemPressed()
        {
            activeItemEditor.Visible = true;
            var skin = ItemBoneMappingScreen.Info.Skin;

            var item = ItemBoneMappingScreen.Info.Skin.ItemAssignments
                           .GetValueOrDefault(ItemBoneMappingScreen.Info.SelectedBone) ??
                       new Item
                       {
                           Name = $"{skin.Name}_{ItemBoneMappingScreen.Info.SelectedBone}_Item",
                           Author = skin.Author,
                           Description = $"Item for bone {ItemBoneMappingScreen.Info.SelectedBone} on skin {skin.Name}",
                           Components = []
                       };

            // Create and setup the item editor overlay
            activeItemEditor.CurrentItem = item;
            activeItemEditor.ItemSaved += OnItemSaved;

            // Fetch the bone transform of the selected bone
            string selectedBoneName = ItemBoneMappingScreen.Info.SelectedBone;
            var boneEntity = GameServices.Store
                .Query<NameEcs, BoneGlobalTransform>()
                .Entities
                .FirstOrDefault(e => e.GetComponent<NameEcs>().Name == selectedBoneName);

            Logger.AddLog(LogCategory.General, boneEntity.GetComponent<BoneGlobalTransform>().GlobalPosition.ToString());

                var boneTransform = boneEntity.GetComponent<BoneGlobalTransform>();
                var transform = new Transform2D(boneTransform.GlobalAngle - Mathf.Pi / 2,boneTransform.GlobalPosition);


                activeItemEditor.Draw += () =>
                {
                    activeItemEditor.SetCanvasTransform(
                        ViewerPanel.ViewerCentre.GetGlobalTransformWithCanvas().TranslatedLocal(ViewerPanel.Size / 2).ScaledLocal(new Vector2(3,3)) * transform
                    );
                };
                activeItemEditor.QueueRedraw();
            // Show/hide buttons
            addItemButton.Visible = false;
            closeEditorButton.Visible = true;
        }

        private void OnCloseEditorPressed()
        {
            activeItemEditor.Visible = false;
            addItemButton.Visible = true;
            closeEditorButton.Visible = false;
        }

        private void OnItemSaved(Item savedItem)
        {
            // Save the item to the skin's bone assignments
            ItemBoneMappingScreen.Info.Skin.ItemAssignments[ItemBoneMappingScreen.Info.SelectedBone] = savedItem;


        }
    }
}
