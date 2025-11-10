using System;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.EntitySystem;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
using XanaduProject.Scenes.ItemEditor;
using XanaduProject.Screens.AssetCreation.ItemEditor.Systems;
using XanaduProject.Stage.Masters.Composer;
using XanaduProject.Utils;
using Mesh = XanaduProject.IO.Mesh;

namespace XanaduProject.Screens.AssetCreation.ItemEditor
{
    public partial class ItemEditor : Control, IItemEditor
    {
        public IComponentLayerManager LayerManager { get; }
        private readonly SystemRoot root = new();

        private Item currentItem { get; set; }
        public event Action<Item>? EditorSaveRequested;

        public event Action? LayersChanged;
        public event Action? ActiveLayerSelectionChanged;


        public Rid CanvasRid => canvasRid;

            private readonly RenderRid canvasRid = RenderRid.Create();

       private PanningCamera camera = new();

       public void SetCanvasTransform(Transform2D transform2D)=>
           canvasLayer.SetTransform(transform2D);

       public Vector2 CanvasMousePosition => canvasLayerContainer.GetGlobalMousePosition();
       public float CanvasAngle => canvasLayer.Rotation;

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


            currentItem = item ?? new Item
            {
                Name = "New Item",
                Author = "Anonymous",
                Description = "A new item."
            };

            LayerManager = new ComponentLayerManager(DiProvider.Get<EntityStore>(), CanvasRid);

            var canvasEntity = GameServices.Store.CreateEntity();
            canvasEntity.Add(new RenderRidComponent(CanvasRid.AsRenderRid()));

            if (currentItem.Components.Count != 0)
            {
                foreach (var component in currentItem.Components)
                {
                    if (component is not Mesh serializableMesh) continue;

                    var entity = DiProvider.Get<EntityStore>().CreateEntity();
                    var oldMeshComponent = serializableMesh.MeshComponent;
                    var newMeshComponent = new MeshComponent
                    {
                        BezierPoints = oldMeshComponent.BezierPoints.ToList(),
                        RenderRid = RenderRid.Create(CanvasRid)
                    };
                    entity.Add(newMeshComponent);
                    LayerManager.AddLayerEntity(entity);
                    MeshUtils.UpdateTriangulation(newMeshComponent);
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

            RenderRid.Create(canvasLayer.GetCanvas())
                .AddLine(new Vector2(0,-100), new Vector2(0,100), Colors.Red);
            RenderRid.Create(canvasLayer.GetCanvas())
                .AddLine(new Vector2(-100,0), new Vector2(100,0), Colors.Red);
        }
        public void TriggerSave()
        {
            var updatedItem = ItemSerializer.CreateItemFromComponentLayerManager(
                LayerManager,
                currentItem.Author,
                currentItem.Description,
                currentItem.Name
            );

            EditorSaveRequested?.Invoke(updatedItem);
        }

        public override void _Process(double delta)
        {
            base._Process(delta);
            canvasLayerContainer.Size = Size;

            root.Update(default);
        }
    }
}
