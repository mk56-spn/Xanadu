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
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref VisualRid visual, ref Layer layer, Entity entity) =>
        {
            CanvasServer.Current.SetParent(visual.Value, _parentCanvas);
            CanvasServer.Current.SetZIndex(visual.Value, layer.ZIndex);
            buffer.RemoveTag<Uninitialised>(entity.Id);
        });
    }
}
