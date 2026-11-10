using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Scrolls the ECS viewport under the pointer. It takes the wheel only when there
/// is one, leaving the notches for the host's own wheel behaviour otherwise.
/// </summary>
public sealed class ScrollSystem(
    IPointerInputSource? pointerSource = null,
    IWheelSource? wheel = null) : QuerySystem<ScrollView, Rectangle, Position>
{
    protected override void OnUpdate()
    {
        if (pointerSource is not null && wheel is { Notches: not 0 })
        {
            Vector2 pointer = pointerSource.Read().Position;
            Entity target = default;
            foreach (Entity entity in Query.ToEntityList())
            {
                Rectangle rect = entity.GetComponent<Rectangle>();
                Position position = entity.GetComponent<Position>();
                Vector2 world = UiTransform.WorldPosition(entity, position.Value);
                var bounds = new Rect2(
                    world + rect.Bounds.Position,
                    rect.Bounds.Size);
                if (bounds.HasPoint(pointer) &&
                    (target.IsNull || LayerZ(entity) > LayerZ(target)))
                {
                    target = entity;
                }
            }

            if (!target.IsNull)
                target.GetComponent<ScrollView>().Offset -= wheel.TakeWheel() * 48f;
        }

        CommandBuffer buffer = CommandBuffer;
        foreach (Entity viewport in Query.ToEntityList())
            Place(viewport, buffer);
    }

    /// <summary>The z index of the layer a viewport draws on, so the topmost one takes the wheel.</summary>
    private static int LayerZ(Entity entity)
    {
        for (; !entity.IsNull; entity = entity.Parent)
        {
            if (entity.TryGetComponent(out Layer layer))
                return layer.ZIndex;
        }
        return int.MinValue;
    }

    /// <summary>Clamps the offset and hangs the content from the viewport's top edge.</summary>
    private static void Place(Entity viewport, CommandBuffer buffer)
    {
        ref ScrollView view = ref viewport.GetComponent<ScrollView>();
        Entity body = viewport.ChildCount > 0 ? viewport.ChildEntities[0] : default;
        if (body.IsNull ||
            !body.HasComponent<Position>() ||
            !body.HasComponent<Rectangle>())
        {
            return;
        }

        float viewportHeight = viewport.GetComponent<Rectangle>().Bounds.Size.Y;
        float contentHeight = body.GetComponent<Rectangle>().Bounds.Size.Y;
        view.Offset = Mathf.Clamp(
            view.Offset,
            0,
            Mathf.Max(0, contentHeight - viewportHeight));

        ref Position content = ref body.GetComponent<Position>();
        float y = -viewportHeight / 2 + contentHeight / 2 - view.Offset;
        if (content.Value.Y == y)
            return;

        content.Value = new Vector2(content.Value.X, y);
        buffer.AddTag<Moved>(body.Id);
    }
}
