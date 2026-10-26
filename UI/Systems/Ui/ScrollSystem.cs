using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Ui;

/// <summary>Scrolls the ECS viewport under the mouse wheel.</summary>
public sealed class ScrollSystem : QuerySystem<ScrollView, Rectangle, Position>
{
    private readonly Queue<(Vector2 Position, int Direction)> _requests = new();

    public void Request(Vector2 position, int direction) =>
        _requests.Enqueue((position, direction));

    protected override void OnUpdate()
    {
        while (_requests.Count > 0)
        {
            (Vector2 pointer, int direction) = _requests.Dequeue();
            foreach (Entity entity in Query.ToEntityList())
            {
                Rectangle rect = entity.GetComponent<Rectangle>();
                Position position = entity.GetComponent<Position>();
                Vector2 world = UiTransform.WorldPosition(entity, position.Value);
                var bounds = new Rect2(
                    world + rect.Bounds.Position,
                    rect.Bounds.Size);
                if (!bounds.HasPoint(pointer))
                    continue;

                ref ScrollView view = ref entity.GetComponent<ScrollView>();
                view.Offset += direction * 48f;
                break;
            }
        }

        CommandBuffer buffer = CommandBuffer;
        foreach (Entity viewport in Query.ToEntityList())
            Place(viewport, buffer);
    }

    /// <summary>Clamps the offset and hangs the content from the viewport's top edge.</summary>
    private static void Place(Entity viewport, CommandBuffer buffer)
    {
        ref ScrollView view = ref viewport.GetComponent<ScrollView>();
        if (view.Content.IsNull ||
            !view.Content.HasComponent<Position>() ||
            !view.Content.HasComponent<Rectangle>())
        {
            return;
        }

        float viewportHeight = viewport.GetComponent<Rectangle>().Bounds.Size.Y;
        float contentHeight = view.Content.GetComponent<Rectangle>().Bounds.Size.Y;
        view.Offset = Mathf.Clamp(
            view.Offset,
            0,
            Mathf.Max(0, contentHeight - viewportHeight));

        ref Position content = ref view.Content.GetComponent<Position>();
        float y = -viewportHeight / 2 + contentHeight / 2 - view.Offset;
        if (content.Value.Y == y)
            return;

        content.Value = new Vector2(content.Value.X, y);
        buffer.AddTag<Moved>(view.Content.Id);
    }
}
