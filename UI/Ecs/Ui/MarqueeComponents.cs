using Friflo.Engine.ECS;
using Godot;

namespace Writer.Ecs;

/// <summary>
/// The screen-space box a view is dragging to select, for its draw system to
/// show. Written from a <c>UiMarquee</c> while a box is active.
/// </summary>
public struct UiMarqueeBox : IComponent
{
    public Vector2 Start;
    public Vector2 Current;
    public bool Active;

    /// <summary>The box between the two corners, whichever way it was dragged.</summary>
    public readonly Rect2 Bounds => Between(Start, Current);

    public static Rect2 Between(Vector2 start, Vector2 current) => new(
        new Vector2(
            System.Math.Min(start.X, current.X),
            System.Math.Min(start.Y, current.Y)),
        new Vector2(
            System.Math.Abs(current.X - start.X),
            System.Math.Abs(current.Y - start.Y)));
}
