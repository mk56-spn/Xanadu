using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Systems.Rendering;

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
        Query.EachEntity(new ApplyVisibility(_canvas, Query.Store, CommandBuffer));
    }

    private readonly record struct ApplyVisibility(IRectCanvas Canvas, EntityStore Store, CommandBuffer Buffer) : IEachEntity<VisualRid>
    {
        public void Execute(ref VisualRid visual, int id)
        {
            Canvas.SetVisible(visual.Value, Store.GetEntityById(id).Enabled);
            Buffer.RemoveTag<VisibilityDirty>(id);
        }
    }
}
