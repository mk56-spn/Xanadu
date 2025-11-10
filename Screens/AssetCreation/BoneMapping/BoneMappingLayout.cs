
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Singletons;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.ECSComponents.Tag;
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
        private HBoxContainer mainHBox { get; } = new();
        private VBoxContainer boneListVBox { get; } = new();
        private readonly AnimatedListControl animatedListControl = new() { ZIndex = 3, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        private readonly Dictionary<string, SelectableButton> boneButtons = new();

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
        }

        private void setupBoneList()
        {
            boneListVBox.SetHSizeFlags(SizeFlags.ShrinkBegin);
            mainHBox.AddChild(boneListVBox);

            var title = new Label { Text = "Bone -> Item Mapping" };
            boneListVBox.AddChild(title);

            refreshBoneList();
            boneListVBox.AddChild(animatedListControl);
        }

        private void refreshBoneList()
        {
            animatedListControl.ClearItems();
            boneButtons.Clear();

            GameServices.Store.Query<NameEcs,BoneGlobalTransform>()
                .ForEachEntity(((ref NameEcs nameEcs,
                ref BoneGlobalTransform _, Entity entity) =>
            {
                bool hasItem = ItemBoneMappingScreen.Info.Skin.ItemAssignments.ContainsKey(nameEcs.Name);
                var color = hasItem ? (Color?)Colors.Green : null;
                var button = animatedListControl.AddItem(nameEcs.Name, () => ItemBoneMappingScreen.Info.SelectedBone = entity, 13, unselectedColor: color);
                boneButtons[nameEcs.Name] = button;
            } ));
        }

        private void setupButtons()
        {
            removeItemButton.Pressed += () =>
            {
                ItemBoneMappingScreen.Info.Skin.ItemAssignments.Remove(ItemBoneMappingScreen.Info.SelectedItemName);
                var v = GameServices.Store.Query<NameEcs>().HasValue<NameEcs, string>(ItemBoneMappingScreen.Info.SelectedItemName).Entities.Single();
                v.GetComponent<CanvasEcs>().Canvas.Free();
                v.RemoveComponent<CanvasEcs>();
                v.RemoveComponent<ItemEcs>();

                refreshBoneList();
            };
            var hBoxButtons = new VBoxContainer();
            hBoxButtons.AddChildren([ addItemButton, removeItemButton]);
            boneListVBox.AddChild(hBoxButtons);

            addItemButton.Pressed += OnAddItemPressed;

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
            var item = ItemBoneMappingScreen.Info.SelectedBone.TryGetComponent(out ItemEcs itemEcs) ? itemEcs.Item : new Item();
            ItemEditor.ItemEditor editor = new ItemEditor.ItemEditor(item);
            ViewerPanel.AddOverlay(editor);

            editor.EditorSaveRequested += i =>
            {

                onEditorSaveRequested(i);
                OnCloseEditorPressed();
                StandardPoseSkinIo.Save(ItemBoneMappingScreen.Info.Skin);

                var buffer = GameServices.Store.GetCommandBuffer();
                GameServices.Store.Query<ItemEcs>().ForEachEntity(((ref ItemEcs _, Entity entity) => buffer.AddTag<Dormant>(entity.Id)));
                buffer.Playback();
                editor.QueueFree();
            };


            boneListVBox.Visible = false;

            // Create and setup the item editor overlay

            var v = ItemBoneMappingScreen.Info.SelectedBone;
            Logger.AddLog(LogCategory.General, ItemBoneMappingScreen.Info.SelectedBone.GetComponent<BoneGlobalTransform>().GlobalPosition.ToString());

                var boneTransform = v.GetComponent<BoneGlobalTransform>();
                var transform = new Transform2D(boneTransform.GlobalAngle - Mathf.Pi / 2,boneTransform.GlobalPosition);


                editor.Draw += () =>
                {
                    editor.SetCanvasTransform(
                        ViewerPanel.ViewerCentre.GetGlobalTransformWithCanvas().TranslatedLocal(ViewerPanel.Size / 2).ScaledLocal(new Vector2(3,3)) * transform
                    );
                };
                editor.QueueRedraw();
            // Show/hide buttons
            addItemButton.Visible = false;


        }

        private void OnCloseEditorPressed()
        {
            Logger.AddLog(LogCategory.General,"Closing editor");
            boneListVBox.Visible = true;
            addItemButton.Visible = true;
        }

        private void onEditorSaveRequested(Item savedItem)
        {
            var buffer = GameServices.Store.GetCommandBuffer();
            if (!ItemBoneMappingScreen.Info.Skin.ItemAssignments.ContainsKey(ItemBoneMappingScreen.Info.SelectedItemName))
            {
                var canvasEcs = new CanvasEcs();
                canvasEcs.Canvas.SetParent(GameServices.Store.Query<RootEcs>().Entities.Single().GetComponent<RootEcs>().Canvas);
                var entity = ItemBoneMappingScreen.Info.SelectedBone;
                        buffer.AddComponent(entity.Id, new ItemEcs(){ Item = savedItem});
                        buffer.AddComponent(entity.Id, canvasEcs);
                        buffer.AddTag<Dormant>(entity.Id);
                ItemBoneMappingScreen.Info.Skin.ItemAssignments[ItemBoneMappingScreen.Info.SelectedItemName] = savedItem;
            }
            else
            {
                ItemBoneMappingScreen.Info.Skin.ItemAssignments[ItemBoneMappingScreen.Info.SelectedItemName].Components = savedItem.Components;
            }

            refreshBoneList();
        }
    }
}
