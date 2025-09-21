using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
using XanaduProject.IO.Indexes;
using XanaduProject.Screens.ScreenStructure;

namespace XanaduProject.Scenes.ItemEditor
{
    public partial class ItemBoneMappingSubScreen : SubScreen
    {
        private readonly StandardPoseSkin skin;
        private readonly string skinName;
        private readonly ItemList boneList;
        private readonly Button addItemButton;
        private readonly Button removeItemButton;
        private readonly Button saveSkinButton;

        private string selectedBone = null!;

        public ItemBoneMappingSubScreen()
        {
            Visible = true;
            var skinInfo = SkinIndex.GetAllSkins().First();
            skin = skinInfo.Value;
            skinName = skinInfo.Key;

            var marginContainer = new MarginContainer();
            marginContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(marginContainer);

            // Main HBoxContainer to hold the bone list section and the pose viewer
            var mainHBox = new HBoxContainer();
            mainHBox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            marginContainer.AddChild(mainHBox);

            // VBoxContainer for the bone list and buttons (left side)
            var boneListVBox = new VBoxContainer();
            boneListVBox.SetHSizeFlags(SizeFlags.ExpandFill);
            mainHBox.AddChild(boneListVBox);

            var title = new Label { Text = "Bone -> Item Mapping" };
            boneListVBox.AddChild(title);

            boneList = new ItemList();
            boneListVBox.AddChild(boneList);
            boneList.SetVSizeFlags(SizeFlags.ExpandFill);

            refreshBoneList();

            var hBoxButtons = new HBoxContainer();
            boneListVBox.AddChild(hBoxButtons);

            addItemButton = new Button { Text = "Add/Edit Item" };
            hBoxButtons.AddChild(addItemButton);
            addItemButton.Disabled = true;

            removeItemButton = new Button { Text = "Remove Item" };
            hBoxButtons.AddChild(removeItemButton);
            removeItemButton.Disabled = true;

            saveSkinButton = new Button { Text = "Save Skin" };
            hBoxButtons.AddChild(saveSkinButton);

            // Pose Viewer (right side)
            var poseViewerPanel = new PanelContainer();
            poseViewerPanel.CustomMinimumSize = new Vector2(400, 0); // Fixed width, expand vertically
            poseViewerPanel.SetHSizeFlags(SizeFlags.ExpandFill);
            poseViewerPanel.SetVSizeFlags(SizeFlags.ExpandFill);
            poseViewerPanel.AddThemeColorOverride("panel_color", new Color(0.15f, 0.15f, 0.2f)); // Darker background
            mainHBox.AddChild(poseViewerPanel);

            // Add a label to indicate it's the pose viewer for now
            var poseLabel = new Label { Text = "Pose Viewer" };
            poseViewerPanel.AddChild(poseLabel);
            poseLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);

            boneList.ItemSelected += OnBoneSelected;
            addItemButton.Pressed += OnAddItemPressed;
            removeItemButton.Pressed += OnRemoveItemPressed;
            saveSkinButton.Pressed += OnSaveSkinPressed;

            SkeletonBuilder.BuildPose();

            DiProvider.Get<EntityStore>().Query<RootEcs>().ForEachEntity((ref RootEcs rootEcs, Entity entity) =>
            {
                var ecs = rootEcs;
                Draw += ()=>
                {
                    ecs.Canvas.SetTransform(new Transform2D(0,new Vector2(3,3),0,poseViewerPanel.Size / 2));
                };
                rootEcs.Canvas.SetParent(poseViewerPanel.GetCanvasItem());
            });
        }

        private void refreshBoneList()
        {
            boneList.Clear();
            foreach (string boneName in skin.BoneNames)
            {
                string displayText = boneName;
                if (skin.ItemAssignments.TryGetValue(boneName, out var item))
                {
                    displayText += $" -> {item.Name}";
                }
                boneList.AddItem(displayText);
            }
        }

        private void OnBoneSelected(long index)
        {
            string? selectedText = boneList.GetItemText((int)index);
            selectedBone = selectedText.Split(" -> ")[0];

            addItemButton.Disabled = false;
            removeItemButton.Disabled = !skin.ItemAssignments.ContainsKey(selectedBone);

            // Get the EntityStore
            var entityStore = DiProvider.Get<EntityStore>();

            // Remove SelectedBoneMarker from any previously selected bone
            entityStore.Query<BoneEcs, SelectedBoneMarker>().ForEachEntity((ref BoneEcs bone, ref SelectedBoneMarker marker, Entity entity) =>
            {
                entity.RemoveComponent<SelectedBoneMarker>();

            });

            var c = entityStore.GetCommandBuffer();

            // Add SelectedBoneMarker to the newly selected bone
            entityStore.Query<BoneEcs>().ForEachEntity((ref BoneEcs bone, Entity entity) =>
            {
                if (entity.TryGetComponent<NameEcs>(out var nameEcs))
                {
                    if (nameEcs.Name == selectedBone)
                    {
                        c.AddComponent<SelectedBoneMarker>(entity.Id);
                    }
                }

            });

            c.Playback();

        }

        private void OnAddItemPressed()
        {
            if (!skin.ItemAssignments.TryGetValue(selectedBone, out var item))
            {
                item = new Item
                {
                    Name = $"{skinName}_{selectedBone}_Item",
                    Author = skin.Author,
                    Description = $"Item for bone {selectedBone} on skin {skinName}",
                    Components = new List<SerializableComponent>() // Changed from MeshLayers
                };
            }

            var itemEditorScreen = new ItemEditorSubScreen(item);
            itemEditorScreen.ItemSaved += (savedItem) =>
            {
                skin.ItemAssignments[selectedBone] = savedItem;
                removeItemButton.Disabled = false;
                refreshBoneList();
            };

            AddChild(itemEditorScreen);
        }

        private void OnRemoveItemPressed()
        {
            if (!skin.ItemAssignments.Remove(selectedBone)) return;
            removeItemButton.Disabled = true;
            refreshBoneList();
        }

        private void OnSaveSkinPressed()
        {
            StandardPoseSkinIo.Save(skin, skinName);
            GD.Print($"Skin \'{skinName}\' saved successfully.");
        }
    }
}
