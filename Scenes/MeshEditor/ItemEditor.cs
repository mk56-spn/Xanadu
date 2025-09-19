using Friflo.Engine.ECS;
using Godot;
using System;
using Friflo.Engine.ECS.Systems;
using XanaduProject.ECSComponents.EntitySystem;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories;
using XanaduProject.Scenes.MeshEditor.Systems;

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class ItemEditor : Control, IItemEditor
    {
        public IMeshLayerManager LayerManager { get; private set; }
        private readonly SystemRoot root = new();

        public Item CurrentItem { get; private set; }
        public event Action<Item> ItemSaved;

        public event Action? MeshLayersChanged;
        public event Action? ActiveMeshSelectionChanged;

        public Rid CanvasRid => GetCanvasItem();

        public ItemEditor(Item? item = null)
        {
            CurrentItem = item ?? new Item
            {
                Name = "New Item",
                Author = "Anonymous",
                Description = "A new item."
            };

            LayerManager = new ItemLayerManager(DiProvider.Get<EntityStore>(), CanvasRid);

            var canvasEntity = DiProvider.Get<EntityStore>().CreateEntity();
            canvasEntity.Add(new RenderRidComponent(CanvasRid.AsRenderRid()));

            if (CurrentItem.MeshLayers.Count != 0)
            {
                foreach (var serializableMeshLayer in CurrentItem.MeshLayers)
                    LayerManager.AddMeshEntity(serializableMeshLayer.MeshComponent);
            }
            else
            {
                LayerManager.AddNewMeshEntity();
            }

            LayerManager.MeshLayersChanged += () => MeshLayersChanged?.Invoke();
            LayerManager.ActiveMeshSelectionChanged += () => ActiveMeshSelectionChanged?.Invoke();

            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(new ItemEditorInput(this));

            var itemEditorUi = new ItemEditorUi(this);
            itemEditorUi.Position = new Vector2(10, 10);
            AddChild(itemEditorUi);

            root.AddStore(DiProvider.Get<EntityStore>());
            root.Add(new MeshVisibilitySystem());
            root.Add(new MeshOutlineRenderSystem(this));
        }

        public void TriggerSave()
        {
            var updatedItem = ItemSerializer.CreateItemFromMeshLayerManager(
                LayerManager,
                CurrentItem.Author,
                CurrentItem.Description,
                CurrentItem.Name
            );

            ItemSaved?.Invoke(updatedItem);
        }

        public override void _Process(double delta)
        {
            base._Process(delta);
            root.Update(default);
        }
    }
}
