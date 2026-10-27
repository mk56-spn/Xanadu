using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;

namespace Writer.Systems.Editor;

/// <summary>
/// The moving parts of a group move's on-canvas target: which member the handle
/// hangs from, where that member sits, and the handle's screen rectangle.
/// </summary>
public static class EditorGroupTarget
{
    /// <summary>The handle's radius in screen pixels.</summary>
    public const float HandleRadius = 14f;

    /// <summary>
    /// The member whose position anchors the handle: the primary selected item
    /// when it is in the group, otherwise the first member.
    /// </summary>
    public static Entity AnchorMember(
        EntityStore entities,
        NoteSelection selection,
        int group)
    {
        Entity selected = selection.Note;
        if (!selected.IsNull &&
            selected.TryGetComponent(out ItemGroups selectedGroups) &&
            selectedGroups.Has(group))
        {
            return selected;
        }

        foreach (Entity entity in entities.Query<ItemGroups>().ToEntityList())
        {
            if (entity.GetComponent<ItemGroups>().Has(group))
                return entity;
        }

        return default;
    }

    /// <summary>The playfield position of an item's centre.</summary>
    public static bool TryPosition(Entity entity, out Vector2 position)
    {
        if (entity.HasComponent<Note>())
        {
            position = entity.GetComponent<Note>().Position;
            return true;
        }

        if (entity.HasComponent<Mine>())
        {
            position = entity.GetComponent<Mine>().Position;
            return true;
        }

        if (entity.HasComponent<Block>())
        {
            position = entity.GetComponent<Block>().Center;
            return true;
        }

        if (entity.HasComponent<StartPoint>())
        {
            position = entity.GetComponent<StartPoint>().Position;
            return true;
        }

        position = default;
        return false;
    }

    public static Rect2 HandleRect(Vector2 center) =>
        new(center - new Vector2(HandleRadius, HandleRadius),
            new Vector2(HandleRadius * 2, HandleRadius * 2));

    /// <summary>Rounds a dragged offset to half a playfield pixel.</summary>
    public static Vector2 Tidy(Vector2 offset) => new(
        Mathf.Round(offset.X * 2f) / 2f,
        Mathf.Round(offset.Y * 2f) / 2f);
}
