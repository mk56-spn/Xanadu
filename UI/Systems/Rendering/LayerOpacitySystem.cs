using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Systems.Canvas;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Rendering;

/// <summary>
/// Applies <see cref="LayerOpacity"/> to the layer's canvas item. Modulate is
/// inherited, so everything under the layer fades with it and nothing redraws.
/// </summary>
public sealed class LayerOpacitySystem : QuerySystem<VisualRid, LayerOpacity>
{
    public LayerOpacitySystem()
    {
        Filter.AllTags(Tags.Get<LayerOpacityDirty>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref VisualRid visual, ref LayerOpacity opacity, Entity entity) =>
        {
            CanvasServer.Current.SetModulate(
                visual.Value,
                new Color(1f, 1f, 1f, opacity.Value));
            buffer.RemoveTag<LayerOpacityDirty>(entity.Id);
        });
    }
}
