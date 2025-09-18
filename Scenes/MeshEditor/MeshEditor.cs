using Friflo.Engine.ECS;
using Godot;
using System;
using System.Linq;
using Friflo.Engine.ECS.Systems;
using XanaduProject.ECSComponents.EntitySystem;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
// Added for Item
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories; // Added for RenderRidComponent

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class MeshEditor : Control, IMeshEditor
    {
        public IMeshLayerManager LayerManager { get; private set; }
        private readonly SystemRoot root = new();

        public event Action? MeshLayersChanged;
        public event Action? ActiveMeshSelectionChanged;

        public Rid CanvasRid => GetCanvasItem();

        public MeshEditor(Item? item = null)
        {
            // Get the MeshLayerManager from the DiProvider
            // Pass the CanvasRid to the MeshLayerManager constructor
            LayerManager = new MeshLayerManager(DiProvider.Get<EntityStore>(), CanvasRid);

            // Create an entity for the MeshEditor's canvas and add the RenderRidComponent
            var canvasEntity = DiProvider.Get<EntityStore>().CreateEntity();
            canvasEntity.Add(new RenderRidComponent(CanvasRid.AsRenderRid()));

            // If an item is provided, load its mesh layers
            if (item != null && item.MeshLayers.Any())
            {
                foreach (var serializableMeshLayer in item.MeshLayers)
                {

                    LayerManager.AddMeshLayer(serializableMeshLayer.MeshData, serializableMeshLayer.Color);
                }
            }
            else
            {
                // If no item is provided or the item has no layers, add a default empty mesh layer
                LayerManager.AddNewMeshLayer();
            }

            // Proxy events from LayerManager
            LayerManager.MeshLayersChanged += () => MeshLayersChanged?.Invoke();
            LayerManager.ActiveMeshSelectionChanged += () => ActiveMeshSelectionChanged?.Invoke();

            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            // Add other systems
            AddChild(new MeshEditorInput(this));
            AddChild(new MeshRenderNode(this));

            // Add the MeshLayerUI
            MeshEditorUi meshEditorUi = new MeshEditorUi(this);
            meshEditorUi.Position = new Vector2(10, 10); // Example position
            AddChild(meshEditorUi);

            root.AddStore(DiProvider.Get<EntityStore>());
            root.Add(new MeshVisibilitySystem()); // Add the new MeshVisibilitySyste);
        }

        public override void _Process(double delta)
        {
            base._Process(delta);
            root.Update(default);
        }
    }
}
