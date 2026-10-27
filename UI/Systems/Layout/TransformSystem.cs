using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Layout;

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
            _canvas.SetTransform(visual.Value, UiTransform.Local(entity, position.Value));
            buffer.RemoveTag<Moved>(entity.Id);
        });
    }
}
