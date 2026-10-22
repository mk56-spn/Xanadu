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
        CommandBuffer buffer = CommandBuffer;

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
                float max = Mathf.Max(0, view.ContentHeight - view.ViewportHeight);
                float next = Mathf.Clamp(view.Offset + direction * 48f, 0, max);
                if (next == view.Offset)
                    break;

                view.Offset = next;
                if (!view.Content.IsNull &&
                    view.Content.HasComponent<Position>())
                {
                    ref Position contentPosition =
                        ref view.Content.GetComponent<Position>();
                    contentPosition.Value = new Vector2(
                        contentPosition.Value.X,
                        view.BaseContentY - view.Offset);
                    buffer.AddTag<Moved>(view.Content.Id);
                }

                break;
            }
        }
    }
}
