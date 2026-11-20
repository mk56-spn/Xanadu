using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Systems.Layout;

/// <summary>Applies local positions to moved canvas items.</summary>
public sealed class TransformSystem : QuerySystem<VisualRid, Position>
{
    private readonly IRectCanvas _canvas;

    public TransformSystem(IRectCanvas canvas)
    {
        _canvas = canvas;
        Filter.AllTags(Tags.Get<Moved>());
    }

    protected override void OnUpdate()
    {
        Query.EachEntity(new ApplyTransforms(_canvas, Query.Store, CommandBuffer));
    }

    private readonly record struct ApplyTransforms(IRectCanvas Canvas, EntityStore Store, CommandBuffer Buffer) : IEachEntity<VisualRid, Position>
    {
        public void Execute(ref VisualRid visual, ref Position position, int id)
        {
            Entity entity = Store.GetEntityById(id);

            Canvas.SetTransform(visual.Value, UiTransform.Local(entity, position.Value));
            Buffer.RemoveTag<Moved>(id);
        }
    }
}
