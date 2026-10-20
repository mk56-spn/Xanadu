using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Systems.Canvas;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Layout;

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
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref visual, ref position, entity) =>
        {
            _canvas.SetTransform(visual.Value, new Transform2D(0, position.Value));
            buffer.RemoveTag<Moved>(entity.Id);
        });
    }
}
