using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Scrolls the ECS viewport or pooled list (<see cref="UiList"/>) under the
/// pointer. It takes the wheel only when there is one, leaving the notches for
/// the host's own wheel behaviour otherwise. Lists are also fitted to their
/// viewports each frame, so a resize shows more or fewer rows.
/// </summary>
public sealed partial class ScrollSystem(
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
                Consider(entity, pointer, ref target);
            foreach (Entity entity in Lists.ToEntityList())
                Consider(entity, pointer, ref target);

            if (!target.IsNull && target.HasComponent<UiList>())
                Scroll(target, wheel.TakeWheel());
            else if (!target.IsNull)
                target.GetComponent<ScrollView>().Offset -= wheel.TakeWheel() * 48f;
        }

        CommandBuffer buffer = CommandBuffer;
        foreach (Entity viewport in Query.ToEntityList())
            Place(viewport, buffer);
        SettleLists(buffer);
    }

    /// <summary>Makes <paramref name="entity"/> the wheel's target when the pointer is over it and it is the topmost so far.</summary>
    private static void Consider(Entity entity, Vector2 pointer, ref Entity target)
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
