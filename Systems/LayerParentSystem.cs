using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

namespace Writer.Systems;

/// <summary>
/// Parents uninitialised layer canvas items to the main control and clears
/// the <see cref="Uninitialised"/> tag.
/// </summary>
public sealed class LayerParentSystem(Rid parentCanvas) : QuerySystem<VisualRid, Layer>
{
    private readonly Rid _parentCanvas = parentCanvas;

    protected override void OnUpdate()
    {
        var buffer = CommandBuffer;

        Query.ForEachEntity((ref VisualRid visual, ref Layer layer, Entity entity) =>
        {
            RenderingServer.CanvasItemSetParent(visual.Value, _parentCanvas);
            RenderingServer.CanvasItemSetZIndex(visual.Value, layer.ZIndex);
            buffer.RemoveTag<Uninitialised>(entity.Id);
        });
    }
}
