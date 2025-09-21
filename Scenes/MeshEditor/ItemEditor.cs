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
using XanaduProject.Stage.Masters.Composer;

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

        public Rid CanvasRid => canvasRid;

        private readonly RenderRid canvasRid = RenderRid.Create();

        private PanningCamera camera = new(); // Declare a field for the camera

        public ItemEditor(Item? item = null)
        {
            CanvasLayer canvasLayer;
            AddChild( canvasLayer = new CanvasLayer()
            {
                FollowViewportEnabled = true,
                Layer = 10
            });
            canvasRid.SetParent(canvasLayer.GetCanvas());

            AddChild(camera); // Add it as a child
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

        public override void _Ready()
        {
            camera.MakeCurrent();
            // Center the camera on the world origin (0,0)
            camera.Position = Vector2.Zero;
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
