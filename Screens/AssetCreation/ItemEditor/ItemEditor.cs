using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.EntitySystem;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
using XanaduProject.Scenes.ItemEditor;
using XanaduProject.Scenes.ItemEditor.Systems;
using XanaduProject.Stage.Masters.Composer;
using XanaduProject.Utils;

namespace XanaduProject.Screens.AssetCreation.ItemEditor
{
    public partial class ItemEditor : Control, IItemEditor
    {
        public IComponentLayerManager LayerManager { get; }
        private readonly SystemRoot root = new();

        public Item CurrentItem { get; set; }
        public event Action<Item>? ItemSaved;

        public event Action? LayersChanged;
        public event Action? ActiveLayerSelectionChanged;

        public Rid CanvasRid => canvasRid;

            private readonly RenderRid canvasRid = RenderRid.Create();

       private PanningCamera camera = new();

       public void SetCanvasTransform(Transform2D transform2D)=>
           canvasLayer.SetTransform(transform2D);

       public Vector2 CanvasTransform => canvasLayerContainer.GetGlobalMousePosition();

       private CanvasLayer canvasLayer;
       private Control canvasLayerContainer;

        public ItemEditor(Item? item = null)
        {
            canvasLayer = new CanvasLayer
            {
                FollowViewportEnabled = true,
                Layer = 10
            };
            canvasRid.SetParent(canvasLayer.GetCanvas());

            canvasLayerContainer = new Control(){ MouseFilter = MouseFilterEnum.Ignore};
            canvasLayerContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            canvasLayer.AddChild(canvasLayerContainer);


            CurrentItem = item ?? new Item
            {
                Name = "New Item",
                Author = "Anonymous",
                Description = "A new item."
            };

            LayerManager = new ComponentLayerManager(DiProvider.Get<EntityStore>(), CanvasRid);

            var canvasEntity = GameServices.Store.CreateEntity();
            canvasEntity.Add(new RenderRidComponent(CanvasRid.AsRenderRid()));

            if (CurrentItem.Components.Count != 0)
            {
                foreach (var component in CurrentItem.Components)
                {
                    if (component is not SerializableMeshComponent serializableMesh) continue;

                    var entity = DiProvider.Get<EntityStore>().CreateEntity();
                    var meshComponent = serializableMesh.MeshComponent;
                    meshComponent.RenderRid = RenderRid.Create(CanvasRid);
                    entity.Add(meshComponent);
                    LayerManager.AddLayerEntity(entity);
                    MeshUtils.UpdateTriangulation(meshComponent);
                }
            }
            else
            {
                LayerManager.AddNewLayerEntity();
            }

            LayerManager.LayersChanged += () => LayersChanged?.Invoke();
            LayerManager.ActiveLayerSelectionChanged += () => ActiveLayerSelectionChanged?.Invoke();

            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            root.AddStore(DiProvider.Get<EntityStore>());
            root.Add(new MeshVisibilitySystem());
            root.Add(new MeshOutlineRenderSystem(this));

            this.AddChildren([
                canvasLayer,
                new ItemEditorInput(this),
                new ItemEditorUi(this) { Position = new Vector2(0,10) },
            ]);
        }

        public override void _Ready()
        {
           /* camera.MakeCurrent();
            camera.Position = Vector2.Zero;*/
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
            canvasLayerContainer.Size = Size;

            root.Update(default);
        }
    }
}
