using Godot;

namespace Xanadu.Ecs;

/// <summary>
/// Where a floating group hugs its parent. <c>Left</c> and <c>Right</c> make a
/// row, <c>Top</c> and <c>Bottom</c> a column; a corner hugs two edges and
/// leaves the flow along the first one.
/// </summary>
public enum UiEdge
{
    Left,
    Right,
    Top,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Center,
}

/// <summary>Maps an <see cref="UiEdge"/> to its anchor point and flow.</summary>
public static class UiEdgeExtensions
{
    /// <summary>The normalized anchor, and pivot, on the parent's bounds.</summary>
    public static Vector2 Anchor(this UiEdge edge) => edge switch
    {
        UiEdge.Left => new Vector2(0, 0.5f),
        UiEdge.Right => new Vector2(1, 0.5f),
        UiEdge.Top => new Vector2(0.5f, 0),
        UiEdge.Bottom => new Vector2(0.5f, 1),
        UiEdge.TopLeft => new Vector2(0, 0),
        UiEdge.TopRight => new Vector2(1, 0),
        UiEdge.BottomLeft => new Vector2(0, 1),
        UiEdge.BottomRight => new Vector2(1, 1),
        _ => new Vector2(0.5f, 0.5f),
    };

    /// <summary>True for the two edges a vertical stack should flow along.</summary>
    public static bool IsVertical(this UiEdge edge) =>
        edge is UiEdge.Top or UiEdge.Bottom;
}
