using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>
/// A screen-read region (docs/render-architecture.md step 11, D10): a placed
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
    public static ScreenReadRegion Centred(Vector2 centre) => new(
        centre - new Vector2(DefaultWidth, DefaultHeight) / 2f,
        new Vector2(DefaultWidth, DefaultHeight));
}
