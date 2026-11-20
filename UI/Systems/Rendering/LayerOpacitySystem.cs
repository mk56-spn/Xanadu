using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Systems.Rendering;

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
        Query.EachEntity(new ApplyOpacity(CommandBuffer));
    }

    private readonly record struct ApplyOpacity(CommandBuffer Buffer) : IEachEntity<VisualRid, LayerOpacity>
    {
        public void Execute(ref VisualRid visual, ref LayerOpacity opacity, int id)
        {
            CanvasServer.Current.SetModulate(
                visual.Value,
                new Color(1f, 1f, 1f, opacity.Value));
            Buffer.RemoveTag<LayerOpacityDirty>(id);
        }
    }
}
