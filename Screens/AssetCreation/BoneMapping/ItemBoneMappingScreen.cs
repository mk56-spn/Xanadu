using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
using XanaduProject.IO.Indexes;
using XanaduProject.Scenes.ItemEditor;
using XanaduProject.Screens.AssetCreation.Skeleton;

namespace XanaduProject.Screens.AssetCreation.BoneMapping
{
    public partial class ItemBoneMappingScreen : PoseScreen
    {
        private readonly StandardPoseSkin skin;
        private readonly string skinName;
        private readonly ItemBoneMappingScreenUi ui = new();

        private string selectedBone = null!;

        private ItemEditorSubScreen itemEditorSubScreen = null!;

        public ItemBoneMappingScreen()
        {
            Visible = true;
            var skinInfo = SkinIndex.GetAllSkins().First();
            skin = skinInfo.Value;
            skinName = skinInfo.Key;



            Root.Add(new EcsDebugSystem());
            Root.Add(new ItemDisplaySystem(ui.ViewerPanel.ViewerCentre, skin));
            Root.Add(new InitializeVisuals(ui.ViewerPanel.ViewerCentre));



            AddChild(ui);

            refreshBoneList();


            refreshVisuals();

            ui.BoneList.ItemSelected += OnBoneSelected;
            ui.AddItemButton.Pressed += OnAddItemPressed;
            ui.RemoveItemButton.Pressed += OnRemoveItemPressed;
            ui.SaveSkinButton.Pressed += OnSaveSkinPressed;
        }

        private void refreshVisuals()
        {
            var command = Store.GetCommandBuffer();
            Store.Query<RootEcs>().ForEachEntity((ref RootEcs rootEcs, Entity entity) =>
                command.AddTag<Dormant>(entity.Id));

            command.Playback();
        }

        private void refreshBoneList()
        {
            ui.BoneList.Clear();
            foreach (string boneName in skin.BoneNames)
            {
                string displayText = boneName;
                if (skin.ItemAssignments.TryGetValue(boneName, out var item))
                {
                    displayText += $" -> {item.Name}";
                }
                ui.BoneList.AddItem(displayText);
            }
        }

        private void OnBoneSelected(long index)
        {
            string? selectedText = ui.BoneList.GetItemText((int)index);
            selectedBone = selectedText.Split(" -> ")[0];

            ui.AddItemButton.Disabled = false;
            ui.RemoveItemButton.Disabled = !skin.ItemAssignments.ContainsKey(selectedBone);

            var entityStore = DiProvider.Get<EntityStore>();


            var command = entityStore.GetCommandBuffer();
            entityStore.Query<BoneEcs, SelectedBoneMarker>().ForEachEntity((ref BoneEcs bone, ref SelectedBoneMarker marker, Entity entity) =>
            {
                command.RemoveComponent<SelectedBoneMarker>(entity.Id);
            });

            entityStore.Query<BoneEcs>().ForEachEntity((ref BoneEcs bone, Entity entity) =>
            {
                if (!entity.TryGetComponent<NameEcs>(out var nameEcs)) return;
                if (nameEcs.Name == selectedBone)
                {
                    command.AddComponent<SelectedBoneMarker>(entity.Id);
                }
            });

            command.Playback();
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

            if (IsInstanceValid(itemEditorSubScreen))
            {
                itemEditorSubScreen.QueueFree();
            }

            itemEditorSubScreen = new ItemEditorSubScreen(item);
            itemEditorSubScreen.SetHSizeFlags(SizeFlags.ExpandFill);
            itemEditorSubScreen.ItemSaved += OnItemEditorSaved;
            itemEditorSubScreen.TreeExited += OnItemEditorClosed;

            ui.BoneListVBox.Visible = false;
            ui.MainHBox.AddChild(itemEditorSubScreen);
            ui.MainHBox.MoveChild(itemEditorSubScreen, ui.BoneListVBox.GetIndex());
        }

        private void OnItemEditorSaved(Item savedItem)
        {
            skin.ItemAssignments[selectedBone] = savedItem;
            ui.RemoveItemButton.Disabled = false;
            refreshBoneList();
        }

        private void OnItemEditorClosed()
        {
            ui.BoneListVBox.Visible = true;
            itemEditorSubScreen = null;
        }

        private void OnRemoveItemPressed()
        {
            if (!skin.ItemAssignments.Remove(selectedBone)) return;
            ui.RemoveItemButton.Disabled = true;
            refreshBoneList();
        }

        private void OnSaveSkinPressed()
        {
            StandardPoseSkinIo.Save(skin, skinName);
            GD.Print($"Skin '{skinName}' saved successfully.");
        }
    }
}
