using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;

namespace Xanadu.Editor;

/// <summary>Which line of the selection's box its items line up on.</summary>
public enum AlignEdge
{
    Left,
    CenterX,
    Right,
    Top,
    CenterY,
    Bottom,
}

/// <summary>
/// Lines a selection's items up with each other: their left, middle or right
/// edges, or their top, middle or bottom edges, on the matching line of the
/// box around them all. Each item moves as a whole along one axis, by the
/// upright box it covers as turned; a note or start point has only a
/// position, so it lines up by that. Nothing leaves the selection's box, so
/// nothing leaves the field.
/// </summary>
public static class SelectionAlign
{
    /// <summary>Whether there is anything to line up: two items or more.</summary>
    public static bool CanAlign(IReadOnlyList<Entity> selection)
    {
        int count = 0;
        foreach (Entity entity in selection)
        {
            if (EditorItems.IsItem(entity) && ItemKinds.Of(entity) is not null && ++count >= 2)
                return true;
        }

        return false;
    }

    /// <summary>Moves every item onto the line; true when any of them moved.</summary>
    public static bool Apply(IReadOnlyList<Entity> selection, AlignEdge edge)
    {
        if (!CanAlign(selection) || !SelectionRotation.TryGetItemBounds(selection, out Rect2 bounds))
            return false;

        bool moved = false;
        foreach (Entity entity in selection)
        {
            if (!EditorItems.IsItem(entity) || ItemKinds.Of(entity) is not { } kind)
                continue;

            Vector2 delta = Offset(kind.Hull(entity), bounds, edge);
            if (delta == Vector2.Zero)
                continue;

            kind.SetPosition(entity, kind.Position(entity) + delta);
            moved = true;
        }

        return moved;
    }

    /// <summary>How far an item's box moves to sit on the selection's line.</summary>
    public static Vector2 Offset(Rect2 item, Rect2 bounds, AlignEdge edge) => edge switch
    {
        AlignEdge.Left => new Vector2(bounds.Position.X - item.Position.X, 0),
        AlignEdge.CenterX => new Vector2(bounds.GetCenter().X - item.GetCenter().X, 0),
        AlignEdge.Right => new Vector2(bounds.End.X - item.End.X, 0),
        AlignEdge.Top => new Vector2(0, bounds.Position.Y - item.Position.Y),
        AlignEdge.CenterY => new Vector2(0, bounds.GetCenter().Y - item.GetCenter().Y),
        AlignEdge.Bottom => new Vector2(0, bounds.End.Y - item.End.Y),
        _ => Vector2.Zero,
    };
}
