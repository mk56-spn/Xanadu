using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

public sealed partial class UiBuilder
{
    /// <summary>
    /// Creates the generic Xanadu.UI radial selector (the cheese wheel) in the
    /// current parent. The wheel paints and hit-tests itself through the
    /// Xanadu.UI systems; callers only supply labels, an initial selection, and
    /// optional per-wedge colours.
    /// </summary>
    public UiWheelControl Wheel(
        Vector2 position,
        float radius,
        IReadOnlyList<string> labels,
        int selected = 0,
        IReadOnlyList<Color>? colors = null,
        float hubRadius = 0.28f,
        float gap = 0.035f,
        float startAngle = UiWheelGeometry.DefaultStartAngle,
        bool takesInput = true)
    {
        ArgumentNullException.ThrowIfNull(labels);
        if (labels.Count == 0)
            throw new ArgumentException("A wheel needs at least one label.", nameof(labels));
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius));

        Entity wheel = RectEntity(
            position,
            new Vector2(radius * 2, radius * 2),
            new Color(0, 0, 0, 0),
            takesInput: takesInput);

        // The wheel paints itself, so it must not also be a RectDrawSystem fill.
        wheel.RemoveComponent<FillColor>();

        // A wheel placed inside a container keeps its explicit polar position.
        if (wheel.Parent.HasComponent<UiContainer>())
            wheel.AddTag<LayoutIgnore>();

        var labelCopy = new string[labels.Count];
        for (int i = 0; i < labels.Count; i++)
            labelCopy[i] = labels[i];

        wheel.AddComponent(new UiWheel
        {
            Selected = UiWheelGeometry.Wrap(selected, labelCopy.Length),
            Hover = -1,
            Labels = labelCopy,
            Colors = colors is null ? null : ToArray(colors),
            HubRadius = Mathf.Clamp(hubRadius, 0.08f, 0.6f),
            StartAngle = startAngle,
            Gap = Mathf.Max(0, gap),
        });
        wheel.AddTag<Dirty>();
        return new UiWheelControl(wheel);
    }

    private static Color[] ToArray(IReadOnlyList<Color> colors)
    {
        var copy = new Color[colors.Count];
        for (int i = 0; i < colors.Count; i++)
            copy[i] = colors[i];
        return copy;
    }
}
