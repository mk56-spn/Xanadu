using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>
/// A row of bars hanging from a polyline, each as tall as its value, such as a
/// spectrum visualizer. The polyline must run left to right; bars are spaced
/// evenly in x and hang from wherever the line is at their x.
/// </summary>
public struct UiBarGraph(
    Vector2[] anchor,
    int count,
    float barWidth,
    float maxHeight,
    Color color,
    Color cap) : IComponent
{
    public Vector2[] Anchor = anchor;
    public float BarWidth = barWidth;
    public float MaxHeight = maxHeight;
    public Color Color = color;

    /// <summary>The bright tip at the free end of every bar.</summary>
    public Color Cap = cap;

    /// <summary>One value per bar, 0 to 1. Whatever drives the graph writes these.</summary>
    public float[] Values = new float[count];
}
