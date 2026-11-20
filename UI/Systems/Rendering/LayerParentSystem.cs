using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Systems.Rendering;

/// <summary>
/// Parents uninitialised layer canvas items to the main control and clears
/// the <see cref="Uninitialised"/> tag.
/// </summary>
public sealed class LayerParentSystem : QuerySystem<VisualRid, Layer>
{
    private readonly Handle _parentCanvas;

    public LayerParentSystem(Handle parentCanvas)
    {
        _parentCanvas = parentCanvas;
        Filter.AllTags(Tags.Get<Uninitialised>());
    }

    protected override void OnUpdate()
    {
        Query.EachEntity(new ParentLayers(_parentCanvas, CommandBuffer));
    }

    private readonly record struct ParentLayers(Handle ParentCanvas, CommandBuffer Buffer) : IEachEntity<VisualRid, Layer>
    {
        public void Execute(ref VisualRid visual, ref Layer layer, int id)
        {
            CanvasServer.Current.SetParent(visual.Value, ParentCanvas);
            CanvasServer.Current.SetZIndex(visual.Value, layer.ZIndex);
            Buffer.RemoveTag<Uninitialised>(id);
        }
    }
}
