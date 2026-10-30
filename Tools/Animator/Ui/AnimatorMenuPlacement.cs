using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tools.Animator;

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

    /// <summary>Hides a menu until it is next placed.</summary>
    public static void Hide(Entity entity) => UiLayout.SetHidden(entity, true);

    /// <summary>Places a menu at its centre while <paramref name="shown"/>, and hides it otherwise.</summary>
    public static void Place(Entity entity, Vector2 position, bool shown)
    {
        if (shown)
            Place(entity, position);
        else
            Hide(entity);
    }

    /// <summary>Shows a menu and moves its centre, flagging it for redraw and its parent for layout.</summary>
    public static void Place(Entity entity, Vector2 position)
    {
        if (entity.IsNull || !entity.HasComponent<Position>())
            return;

        UiLayout.SetHidden(entity, false);
        ref Position current = ref entity.GetComponent<Position>();
        if (current.Value == position)
            return;

        current.Value = position;
        entity.AddTag<Moved>();
        entity.AddTag<Dirty>();
    }
}
