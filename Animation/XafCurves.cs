using System;

namespace Xanadu.Animation;

/// <summary>A family of named easing curves: how the change is shaped, before its direction.</summary>
public enum XafCurveFamily
{
    Sine,
    Quad,
    Cubic,
    Quart,
    Expo,
    Circ,
    Back,
    Elastic,
    Bounce,
}

/// <summary>Which end of the span a curve slows: the start, the end, or both.</summary>
public enum XafCurveDirection
{
    In,
    Out,
    InOut,
}

/// <summary>
/// The named easing curves a key can leave with, as progress from 0 to 1
/// across the span. Back and elastic overshoot past both ends, so they can
/// return values outside 0..1 by design.
/// </summary>
public static class XafCurves
{
    /// <summary>How far back overshoots: the classic 10%.</summary>
    private const float BackOvershoot = 1.70158f;

    public static float Evaluate(XafCurveFamily family, XafCurveDirection direction, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return direction switch
        {
            XafCurveDirection.In => In(family, t),
            XafCurveDirection.Out => 1f - In(family, 1f - t),
            _ => t < 0.5f
                ? In(family, t * 2f) / 2f
                : 1f - In(family, (1f - t) * 2f) / 2f,
        };
    }

    /// <summary>The ease-in form of each family; out and in-out are built from it.</summary>
    private static float In(XafCurveFamily family, float t) => family switch
    {
        XafCurveFamily.Sine => 1f - MathF.Cos(t * MathF.PI / 2f),
        XafCurveFamily.Quad => t * t,
        XafCurveFamily.Cubic => t * t * t,
        XafCurveFamily.Quart => t * t * t * t,
        XafCurveFamily.Expo => t <= 0f ? 0f : MathF.Pow(2f, 10f * t - 10f),
        XafCurveFamily.Circ => 1f - MathF.Sqrt(Math.Max(0f, 1f - t * t)),
        XafCurveFamily.Back => t * t * ((BackOvershoot + 1f) * t - BackOvershoot),
        XafCurveFamily.Elastic => t <= 0f ? 0f : t >= 1f ? 1f
            : -MathF.Pow(2f, 10f * t - 10f) * MathF.Sin((t * 10f - 10.75f) * (2f * MathF.PI / 3f)),
        XafCurveFamily.Bounce => 1f - BounceOut(1f - t),
        _ => t,
    };

    private static float BounceOut(float t)
    {
        const float n = 7.5625f;
        const float d = 2.75f;
        if (t < 1f / d)
            return n * t * t;
        if (t < 2f / d)
            return n * (t -= 1.5f / d) * t + 0.75f;
        if (t < 2.5f / d)
            return n * (t -= 2.25f / d) * t + 0.9375f;
        return n * (t -= 2.625f / d) * t + 0.984375f;
    }
}
