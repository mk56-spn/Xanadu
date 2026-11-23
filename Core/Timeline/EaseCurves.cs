namespace Xanadu.Core.Timeline;

/// <summary>A family of named easing curves: how the change is shaped, before its direction.</summary>
public enum CurveFamily
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
public enum CurveDirection
{
    In,
    Out,
    InOut,
}

/// <summary>
/// The named easing curves, as progress from 0 to 1 across a span. Shared by
/// every timeline: animator keys, group effects and shader keys. Back and
/// elastic overshoot past both ends, so they can return values outside 0..1
/// by design.
/// </summary>
public static class EaseCurves
{
    /// <summary>How far back overshoots: the classic 10%.</summary>
    private const float BackOvershoot = 1.70158f;

    public static float Evaluate(CurveFamily family, CurveDirection direction, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return direction switch
        {
            CurveDirection.In => In(family, t),
            CurveDirection.Out => 1f - In(family, 1f - t),
            _ => t < 0.5f
                ? In(family, t * 2f) / 2f
                : 1f - In(family, (1f - t) * 2f) / 2f,
        };
    }

    /// <summary>
    /// A cubic Hermite segment from <paramref name="from"/> to <paramref name="to"/>
    /// at share <paramref name="t"/> of a <paramref name="span"/> long stretch,
    /// leaving and arriving with the given slopes (value per unit of time). The
    /// animator's keys draw their ease, ease in and ease out with it.
    /// </summary>
    public static float Hermite(float from, float to, float slopeOut, float slopeIn, float t, float span)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        float h00 = 2f * t3 - 3f * t2 + 1f;
        float h10 = t3 - 2f * t2 + t;
        float h01 = -2f * t3 + 3f * t2;
        float h11 = t3 - t2;
        return h00 * from + h10 * slopeOut * span + h01 * to + h11 * slopeIn * span;
    }

    /// <summary>The ease-in form of each family; out and in-out are built from it.</summary>
    private static float In(CurveFamily family, float t) => family switch
    {
        CurveFamily.Sine => 1f - MathF.Cos(t * MathF.PI / 2f),
        CurveFamily.Quad => t * t,
        CurveFamily.Cubic => t * t * t,
        CurveFamily.Quart => t * t * t * t,
        CurveFamily.Expo => t <= 0f ? 0f : MathF.Pow(2f, 10f * t - 10f),
        CurveFamily.Circ => 1f - MathF.Sqrt(Math.Max(0f, 1f - t * t)),
        CurveFamily.Back => t * t * ((BackOvershoot + 1f) * t - BackOvershoot),
        CurveFamily.Elastic => t <= 0f ? 0f : t >= 1f ? 1f
            : -MathF.Pow(2f, 10f * t - 10f) * MathF.Sin((t * 10f - 10.75f) * (2f * MathF.PI / 3f)),
        CurveFamily.Bounce => 1f - BounceOut(1f - t),
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
