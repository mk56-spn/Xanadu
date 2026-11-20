using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Systems.Layout;

/// <summary>Clears <see cref="Moved"/> once a frame has seen it; where things draw is read live by <see cref="Rendering.UiCanvasMount"/>.</summary>
public sealed class TransformSystem : QuerySystem<VisualItem, Position>
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

    private readonly record struct ApplyTransforms(IRectCanvas Canvas, EntityStore Store, CommandBuffer Buffer) : IEachEntity<VisualItem, Position>
    {
        public void Execute(ref VisualItem visual, ref Position position, int id)
        {
            Buffer.RemoveTag<Moved>(id);
        }
    }
}
