using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// A screen-read region (Docs/render-architecture.md step 11, D10): a placed
/// rect with a layer and z. When the draw order reaches it, the back buffer is
/// refreshed for its rect, so screen-reading shaders drawn after it see the
/// pixels up to that point. It draws no body of its own; the editor outlines
/// it.
/// </summary>
public struct ScreenReadRegion(Vector2 position, Vector2 size) : IComponent
{
    public const float DefaultWidth = 600f;
    public const float DefaultHeight = 400f;

    /// <summary>The top-left corner, in playfield units.</summary>
    public Vector2 Position = position;

    /// <summary>The rect's size, in playfield units.</summary>
    public Vector2 Size = size;

    public readonly Rect2 Bounds => new(Position, Size);

    public readonly bool Contains(Vector2 point) => Bounds.HasPoint(point);

    /// <summary>A default-sized region centred on a playfield point.</summary>
    public static ScreenReadRegion Centred(Vector2 centre) =>
        Centred(centre, new Vector2(DefaultWidth, DefaultHeight));

    /// <summary>A region of <paramref name="size"/> centred on a playfield point.</summary>
    public static ScreenReadRegion Centred(Vector2 centre, Vector2 size) => new(centre - size / 2f, size);

    /// <summary>
    /// The size a new region starts at: the default, or a third of what the
    /// view shows when that's smaller, so a fresh region fits in view.
    /// <paramref name="visible"/> is the view's size in playfield units.
    /// </summary>
    public static Vector2 StartSize(Vector2 visible) => new(
        Mathf.Min(DefaultWidth, Mathf.Round(visible.X / 3f)),
        Mathf.Min(DefaultHeight, Mathf.Round(visible.Y / 3f)));
}
