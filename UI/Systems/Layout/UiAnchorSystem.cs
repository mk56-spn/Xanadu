using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Layout;

/// <summary>Sticks anchored children to their parent without taking part in layout.</summary>
public sealed class UiAnchorSystem : QuerySystem<UiAnchor, Rectangle, Position>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref anchor, ref rect, ref position, entity) =>
        {
            Entity parent = entity.Parent;
            if (parent.IsNull || !parent.HasComponent<Rectangle>())
                return;

            Rectangle parentRect = parent.GetComponent<Rectangle>();
            Vector2 size = rect.Bounds.Size;
            Vector2 target =
                parentRect.Bounds.Position +
                parentRect.Bounds.Size * anchor.Anchor -
                size * anchor.Pivot +
                size / 2;

            if (position.Value == target)
                return;

            position.Value = target;
            buffer.AddTag<Moved>(entity.Id);
        });
    }
}
