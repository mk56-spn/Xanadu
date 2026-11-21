using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// Turns, scales and mirrors a drawable about a pivot in its own space (relative
/// to its <see cref="Core.Position"/>), on top of its own rotation: so several
/// separate entities can be posed as one group about a shared point. A negative
/// scale mirrors. Drawing only; hit tests use the unposed bounds.
/// </summary>
public struct UiPose(Vector2 pivot, Vector2 scale, float radians) : IComponent
{
    public Vector2 Pivot = pivot;
    public Vector2 Scale = scale;
    public float Radians = radians;

    /// <summary>The pose as a transform: p' = pivot + R S (p - pivot).</summary>
    public readonly Transform2D Around()
    {
        float cos = MathF.Cos(Radians), sin = MathF.Sin(Radians);
        var x = new Vector2(cos, sin) * Scale.X;
        var y = new Vector2(-sin, cos) * Scale.Y;
        return new Transform2D(x, y, Pivot - (x * Pivot.X + y * Pivot.Y));
    }
}
