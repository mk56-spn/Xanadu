using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Systems.Layout;

/// <summary>Sticks anchored children to their parent without taking part in layout.</summary>
public sealed class UiAnchorSystem : QuerySystem<UiAnchor, Rectangle, Position>
{
    protected override void OnUpdate()
    {
        Query.EachEntity(new PlaceAnchored(Query.Store, CommandBuffer));
    }

    private readonly record struct PlaceAnchored(EntityStore Store, CommandBuffer Buffer) : IEachEntity<UiAnchor, Rectangle, Position>
    {
        public void Execute(ref UiAnchor anchor, ref Rectangle rect, ref Position position, int id)
        {
            Entity entity = Store.GetEntityById(id);

            Entity parent = entity.Parent;
            if (parent.IsNull || !parent.HasComponent<Rectangle>())
                return;

            Rectangle parentRect = parent.GetComponent<Rectangle>();
            Vector2 size = rect.Bounds.Size;
            Vector2 target =
                parentRect.Bounds.Position +
                parentRect.Bounds.Size * anchor.Anchor -
                size * anchor.Pivot +
                size / 2 +
                anchor.Inset;

            if (position.Value == target)
                return;

            position.Value = target;
            Buffer.AddTag<Moved>(id);
        }
    }
}
