using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Rendering;

/// <summary>Shows and hides canvas items as their entities are enabled and disabled.</summary>
public sealed class CanvasVisibilitySystem : QuerySystem<VisualRid>
{
    private readonly IRectCanvas _canvas;

    public CanvasVisibilitySystem(IRectCanvas canvas)
    {
        _canvas = canvas;
        Filter.AllTags(Tags.Get<VisibilityDirty>());
        Filter.WithDisabled();
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        Query.ForEachEntity((ref VisualRid visual, Entity entity) =>
        {
            _canvas.SetVisible(visual.Value, entity.Enabled);
            buffer.RemoveTag<VisibilityDirty>(entity.Id);
        });
    }
}
