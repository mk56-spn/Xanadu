using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Tools.Animator;

/// <summary>
/// Placement shared by the animator's floating menus: keeping them on screen,
/// and moving them without redoing the draw and layout work every frame.
/// </summary>
public static class AnimatorMenuPlacement
{
    /// <summary>
    /// Top-left corner for a menu opened at a point: below and right of it,
    /// flipped above or left where there is no room, and always on screen.
    /// </summary>
    public static Vector2 Anchor(Vector2 point, Vector2 size, Vector2 screen)
    {
        float x = point.X + size.X > screen.X ? point.X - size.X : point.X;
        float y = point.Y + size.Y > screen.Y ? point.Y - size.Y : point.Y;
        return new Vector2(
            Mathf.Clamp(x, 0f, Mathf.Max(0f, screen.X - size.X)),
            Mathf.Clamp(y, 0f, Mathf.Max(0f, screen.Y - size.Y)));
    }

    /// <summary>Centres the menu on a top-left point, touching it only when it moves.</summary>
    public static void Place(Entity entity, Vector2 topLeft, Vector2 size) =>
        Place(entity, topLeft + size / 2);

    /// <summary>Moves a menu's centre, flagging it for redraw and its parent for layout.</summary>
    public static void Place(Entity entity, Vector2 position)
    {
        if (entity.IsNull || !entity.HasComponent<Position>())
            return;

        ref Position current = ref entity.GetComponent<Position>();
        if (current.Value == position)
            return;

        current.Value = position;
        entity.AddTag<Moved>();
        entity.AddTag<Dirty>();
    }
}
