using System;
using System.Collections.Generic;
using Xanadu.Core.Maths;

namespace Xanadu.Ui.Systems.Canvas;

/// <summary>
/// Curve tags on triangles: a tagged triangle's corners are a quadratic
/// Bézier's start, control and end, and the GPU fills the part on one side of
/// the curve (u² − v, Loop–Blinn). The CPU never samples the curve.
/// </summary>
public static class CanvasCurves
{
    /// <summary>A plain triangle.</summary>
    public const byte None = 0;

    /// <summary>Filled between the chord and the curve: the curve bulges out of the shape.</summary>
    public const byte Convex = 1;

    /// <summary>Filled between the curve and the control: the curve bends into the shape.</summary>
    public const byte Concave = 2;

    /// <summary>
    /// The fallback for a canvas that cannot trim: convex curve triangles are
    /// dropped and concave ones drawn whole, so each curve shows as its chord.
    /// </summary>
    public static (Vector2[] Points, Color[] Colors, Vector2[] Uvs) Straighten(
        ReadOnlySpan<Vector2> points, ReadOnlySpan<Color> colors, ReadOnlySpan<Vector2> uvs, ReadOnlySpan<byte> curves)
    {
        var kept = new List<Vector2>(points.Length);
        var keptColors = new List<Color>(points.Length);
        var keptUvs = new List<Vector2>(points.Length);
        for (int i = 0; i + 2 < points.Length; i += 3)
        {
            if (i / 3 < curves.Length && curves[i / 3] == Convex)
                continue;
            for (int k = 0; k < 3; k++)
            {
                kept.Add(points[i + k]);
                keptColors.Add(i + k < colors.Length ? colors[i + k] : colors[^1]);
                keptUvs.Add(i + k < uvs.Length ? uvs[i + k] : default);
            }
        }
        return (kept.ToArray(), keptColors.ToArray(), keptUvs.ToArray());
    }
}
