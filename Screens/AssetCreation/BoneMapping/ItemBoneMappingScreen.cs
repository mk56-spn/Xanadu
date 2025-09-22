using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
using XanaduProject.IO.Indexes;
using XanaduProject.Screens.AssetCreation.BoneMapping;
using XanaduProject.Screens.AssetCreation.Skeleton;
using XanaduProject.Screens.ScreenStructure;

namespace XanaduProject.Scenes.ItemEditor.BoneMapping
{
    public partial class ItemBoneMappingScreen : MainScreen
    {
        private readonly SystemRoot root;
        private readonly StandardPoseSkin skin;
        private readonly string skinName;
        private readonly ItemList boneList;
        private readonly Button addItemButton;
        private readonly Button removeItemButton;
        private readonly Button saveSkinButton;

        private string selectedBone = null!;

        // New members for managing the layout
        private readonly HBoxContainer mainHBox;
        private readonly VBoxContainer boneListVBox;
        private ItemEditorSubScreen itemEditorSubScreen = null!; // This will hold the current item editor instance

        public ItemBoneMappingScreen(SystemRoot root)
        {
            this.root = root;

            Visible = true;
            var skinInfo = SkinIndex.GetAllSkins().First();
            skin = skinInfo.Value;
            skinName = skinInfo.Key;

            var marginContainer = new MarginContainer();
            marginContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(marginContainer);

            // Main HBoxContainer to hold the bone list section and the pose viewer
            mainHBox = new HBoxContainer(); // Assign to member
            mainHBox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            marginContainer.AddChild(mainHBox);

            // VBoxContainer for the bone list and buttons (left side)
            boneListVBox = new VBoxContainer(); // Assign to member
            boneListVBox.SetHSizeFlags(SizeFlags.ExpandFill);
            mainHBox.AddChild(boneListVBox); // This is the first child of mainHBox

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

            // Skeleton Viewer (right side)

            boneList.ItemSelected += OnBoneSelected;
            addItemButton.Pressed += OnAddItemPressed;
            removeItemButton.Pressed += OnRemoveItemPressed;
            saveSkinButton.Pressed += OnSaveSkinPressed;

            SkeletonBuilder.BuildPose();

            setupViewer();
        }

        private void setupViewer()
        {
            var poseViewerPanel = new PanelContainer();
            poseViewerPanel.CustomMinimumSize = new Vector2(200, 0); // Fixed width, expand vertically
            poseViewerPanel.SetHSizeFlags(SizeFlags.ShrinkEnd);
            poseViewerPanel.SetVSizeFlags(SizeFlags.ExpandFill);
            poseViewerPanel.AddThemeColorOverride("panel_color", new Color(0.15f, 0.15f, 0.2f)); // Darker background

            mainHBox.AddChild(poseViewerPanel);

            // Add a label to indicate it's the pose viewer for now
            var poseLabel = new Label { Text = "Skeleton Viewer" };
            poseViewerPanel.AddChild(poseLabel);
            poseLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);

            Control viewerCentre = new Control();
            poseViewerPanel.AddChild(viewerCentre);
            poseViewerPanel.Draw += () => viewerCentre.Position = poseViewerPanel.Size / 2;
            DiProvider.Get<EntityStore>().Query<RootEcs>().ForEachEntity((ref RootEcs rootEcs, Entity _) =>
            {
                rootEcs.Canvas.SetParent(viewerCentre.GetCanvasItem());
            });

            root.Add(new ItemDisplaySystem(viewerCentre));
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
                if (!entity.TryGetComponent<NameEcs>(out var nameEcs)) return;
                if (nameEcs.Name == selectedBone)
                {
                    c.AddComponent<SelectedBoneMarker>(entity.Id);
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
                    Components = new List<SerializableComponent>()
                };
            }

            // If an item editor is already open, ensure it's cleaned up before opening a new one.
            if (IsInstanceValid(itemEditorSubScreen))
            {
                itemEditorSubScreen.QueueFree(); // This will trigger OnItemEditorClosed
            }

            // Create the ItemEditorSubScreen
            itemEditorSubScreen = new ItemEditorSubScreen(item);
            itemEditorSubScreen.SetHSizeFlags(SizeFlags.ExpandFill); // Ensure it takes up space

            // Connect to wthe ItemSaved event
            itemEditorSubScreen.ItemSaved += OnItemEditorSaved;

            // Connect to TreeExited to handle when the editor is closed/freed (e.g., by a "Cancel" button within the editor)
            itemEditorSubScreen.TreeExited += OnItemEditorClosed;

            // Hide the bone list and add the item editor
            boneListVBox.Visible = false;
            mainHBox.AddChild(itemEditorSubScreen); // Add it to the mainHBox
            mainHBox.MoveChild(itemEditorSubScreen, boneListVBox.GetIndex()); // Place it at the same position as boneListVBox
        }

        private void OnItemEditorSaved(Item savedItem)
        {
            skin.ItemAssignments[selectedBone] = savedItem;
            removeItemButton.Disabled = false;
            refreshBoneList();

            // The editor will likely QueueFree() itself after saving, triggering OnItemEditorClosed.
            // If it doesn't, we would need to manually handle its removal/hiding here.
            // For now, rely on OnItemEditorClosed to make boneListVBox visible again.
        }

        private void OnItemEditorClosed()
        {
            // This is called when _itemEditorSubScreen is removed from the scene tree (e.g., QueueFree'd)
            // Make the bone list visible again
            boneListVBox.Visible = true;
            itemEditorSubScreen = null; // Clear the reference
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
                GD.Print($"Skin '{skinName}' saved successfully.");
        }
    }
}
