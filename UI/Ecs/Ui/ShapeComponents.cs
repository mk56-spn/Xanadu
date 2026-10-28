using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>
/// A convex polygon or open polyline in the entity's local space, with an
/// optional fill and stroke. A transparent colour skips that part.
/// </summary>
public struct UiShape(
    Vector2[] points,
    Color fill,
    Color stroke,
    float strokeWidth = 0,
    bool closed = true) : IComponent
{
    public Vector2[] Points = points;
    public Color Fill = fill;
    public Color Stroke = stroke;
    public float StrokeWidth = strokeWidth;
    public bool Closed = closed;
}

/// <summary>
/// Soft glow drawn behind a shape: a gaussian-blurred copy of its fill and
/// stroke in <see cref="Color"/>. Works on any entity with <see cref="UiShape"/>.
/// </summary>
public struct ShapeGlow(Color color, float radius = 12f, float intensity = 1.5f) : IComponent
{
    public Color Color = color;
    public float Radius = radius;
    public float Intensity = intensity;
}
