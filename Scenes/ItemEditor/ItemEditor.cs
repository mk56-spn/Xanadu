using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.EntitySystem;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
using XanaduProject.Scenes.ItemEditor.Systems;
using XanaduProject.Stage.Masters.Composer;
using XanaduProject.Utils;

namespace XanaduProject.Scenes.ItemEditor
{
    public partial class ItemEditor : Control, IItemEditor
    {
        public IComponentLayerManager LayerManager { get; private set; }
        private readonly SystemRoot root = new();

        public Item CurrentItem { get; private set; }
        public event Action<Item> ItemSaved;

        public event Action? LayersChanged;
        public event Action? ActiveLayerSelectionChanged;

        public Rid CanvasRid => canvasRid;

        private readonly RenderRid canvasRid = RenderRid.Create();

        private PanningCamera camera = new();

        public ItemEditor(Item? item = null)
        {
            var canvasLayer = new CanvasLayer
            {
                FollowViewportEnabled = true,
                Layer = 10
            };
            AddChild(canvasLayer);
            canvasRid.SetParent(canvasLayer.GetCanvas());

            AddChild(camera);
            CurrentItem = item ?? new Item
            {
                Name = "New Item",
                Author = "Anonymous",
                Description = "A new item."
            };

            LayerManager = new ComponentLayerManager(DiProvider.Get<EntityStore>(), CanvasRid);

            var canvasEntity = DiProvider.Get<EntityStore>().CreateEntity();
            canvasEntity.Add(new RenderRidComponent(CanvasRid.AsRenderRid()));

            if (CurrentItem.Components.Count != 0)
            {
                foreach (var component in CurrentItem.Components)
                {
                    if (component is SerializableMeshComponent serializableMesh)
                    {
                        var entity = DiProvider.Get<EntityStore>().CreateEntity();
                        var meshComponent = serializableMesh.MeshComponent;
                        meshComponent.RenderRid = RenderRid.Create(CanvasRid);
                        entity.Add(meshComponent);
                        LayerManager.AddLayerEntity(entity);
                        MeshUtils.UpdateTriangulation(meshComponent);
                    }
                }
            }
            else
            {
                LayerManager.AddNewLayerEntity();
            }

            LayerManager.LayersChanged += () => LayersChanged?.Invoke();
            LayerManager.ActiveLayerSelectionChanged += () => ActiveLayerSelectionChanged?.Invoke();

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
            camera.Position = Vector2.Zero;
        }

        public void TriggerSave()
        {
            var updatedItem = ItemSerializer.CreateItemFromComponentLayerManager(
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
