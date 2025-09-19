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

        public event Action? MeshLayersChanged;
        public event Action? ActiveMeshSelectionChanged;

        public Rid CanvasRid => GetCanvasItem();

        public ItemEditor(Item? item = null)
        {
            // Get the ItemLayerManager from the DiProvider
            // Pass the CanvasRid to the ItemLayerManager constructor
            LayerManager = new ItemLayerManager(DiProvider.Get<EntityStore>(), CanvasRid);

            // Create an entity for the ItemEditor's canvas and add the RenderRidComponent
            var canvasEntity = DiProvider.Get<EntityStore>().CreateEntity();
            canvasEntity.Add(new RenderRidComponent(CanvasRid.AsRenderRid()));

            // If an item is provided, load its mesh layers
            if (item != null && item.MeshLayers.Count != 0)
            {
                foreach (var serializableMeshLayer in item.MeshLayers)
                    LayerManager.AddMeshEntity(serializableMeshLayer.MeshComponent);
            }
            else
            {
                // If no item is provided or the item has no layers, add a default empty mesh layer
                LayerManager.AddNewMeshEntity();
            }

            // Proxy events from LayerManager
            LayerManager.MeshLayersChanged += () => MeshLayersChanged?.Invoke();
            LayerManager.ActiveMeshSelectionChanged += () => ActiveMeshSelectionChanged?.Invoke();

            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            // Add other systems
            AddChild(new ItemEditorInput(this));



            // Add the MeshLayerUI
            ItemEditorUi itemEditorUi = new ItemEditorUi(this);
            itemEditorUi.Position = new Vector2(10, 10); // Example position
            AddChild(itemEditorUi);

            root.AddStore(DiProvider.Get<EntityStore>());
            root.Add(new MeshVisibilitySystem()); // Add the new MeshVisibilitySyste);
            root.Add(new MeshOutlineRenderSystem(this));
        }

        public override void _Process(double delta)
        {
            base._Process(delta);
            root.Update(default);
        }
    }
}
