using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

/// <summary>Four thin edges that outline the focused control on a top layer.</summary>
public sealed class UiFocusRing
{
    private const float RingWidth = 2f;
    private readonly Entity[] _edges = new Entity[4];

    public UiFocusRing(EntityStore entities, IVisualRidFactory visuals)
    {
        UiBuilder ui = UiBuilder.Begin(entities, visuals).Layer(UiTheme.FocusLayer);
        for (int i = 0; i < _edges.Length; i++)
        {
            Entity edge = ui.RectEntity(
                UiLayout.Hidden,
                new Vector2(RingWidth, RingWidth),
                UiTheme.Focus);
            edge.AddComponent(new UiFocusRingEdge(i));
            _edges[i] = edge;
        }
    }

    /// <summary>Moves the ring around <paramref name="target"/>.</summary>
    public void Show(Entity target, CommandBuffer buffer)
    {
        Rectangle rect = target.GetComponent<Rectangle>();
        Vector2 world = UiTransform.WorldPosition(
            target,
            target.GetComponent<Position>().Value);
        Rect2 outer = new Rect2(
            world + rect.Bounds.Position,
            rect.Bounds.Size).Grow(RingWidth);

        SetEdge(_edges[0], new Vector2(outer.GetCenter().X, outer.Position.Y),
            new Vector2(outer.Size.X, RingWidth), buffer);
        SetEdge(_edges[1], new Vector2(outer.GetCenter().X, outer.End.Y - RingWidth),
            new Vector2(outer.Size.X, RingWidth), buffer);
        SetEdge(_edges[2], new Vector2(outer.Position.X, outer.GetCenter().Y),
            new Vector2(RingWidth, outer.Size.Y), buffer);
        SetEdge(_edges[3], new Vector2(outer.End.X - RingWidth, outer.GetCenter().Y),
            new Vector2(RingWidth, outer.Size.Y), buffer);
    }

    public void Hide(CommandBuffer buffer)
    {
        foreach (Entity edge in _edges)
            Hide(edge, buffer);
    }

    private static void SetEdge(
        Entity edge,
        Vector2 position,
        Vector2 size,
        CommandBuffer buffer)
    {
        if (edge.IsNull)
            return;

        ref Rectangle rectangle = ref edge.GetComponent<Rectangle>();
        if (rectangle.Bounds.Size != size)
        {
            rectangle.Bounds = new Rect2(-size / 2, size);
            buffer.AddTag<Dirty>(edge.Id);
        }

        ref Position edgePosition = ref edge.GetComponent<Position>();
        if (edgePosition.Value != position)
        {
            edgePosition.Value = position;
            buffer.AddTag<Moved>(edge.Id);
        }
    }

    private static void Hide(Entity edge, CommandBuffer buffer)
    {
        if (edge.IsNull || !edge.HasComponent<Position>())
            return;

        ref Position position = ref edge.GetComponent<Position>();
        if (position.Value == UiLayout.Hidden)
            return;

        position.Value = UiLayout.Hidden;
        buffer.AddTag<Moved>(edge.Id);
    }
}
