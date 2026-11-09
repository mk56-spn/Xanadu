using System;

namespace Xanadu.Timeline;

/// <summary>
/// How a timed change travels from its start to its end: the segment leaving
/// an animator key, a group effect, a shader key. Stored as a byte in XAF and
/// XMF, so values keep their order.
/// </summary>
public enum EasePreset
{
    Step,
    Linear,

    /// <summary>Slow at both ends.</summary>
    Ease,

    /// <summary>Slow start, full speed at the end.</summary>
    EaseIn,

    /// <summary>Full speed at the start, slow end.</summary>
    EaseOut,

    // Named curves; the animator stores them as the XafInterpolation of the same name.
    SineIn,
    SineOut,
    SineInOut,
    QuadInOut,
    CubicIn,
    CubicOut,
    CubicInOut,
    QuartIn,
    QuartOut,
    QuartInOut,
    ExpoIn,
    ExpoOut,
    ExpoInOut,
    CircIn,
    CircOut,
    CircInOut,
    BackIn,
    BackOut,
    BackInOut,
    ElasticIn,
    ElasticOut,
    ElasticInOut,
    BounceIn,
    BounceOut,
    BounceInOut,
}

/// <summary>The maths and naming of <see cref="EasePreset"/>, shared by every timeline.</summary>
public static class EasePresets
{
    /// <summary>
    /// How far through its change a preset is at a share <paramref name="t"/>
    /// of the span, clamped to 0..1: 0 to 1, except back and elastic, which overshoot.
    /// </summary>
    public static float Progress(EasePreset preset, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return preset switch
        {
            EasePreset.Step => t >= 1f ? 1f : 0f,
            EasePreset.Linear => t,
            EasePreset.EaseIn => t * t,
            EasePreset.EaseOut => t * (2f - t),
            EasePreset.Ease => t * t * (3f - 2f * t),
            _ => Shape(preset) is var (family, direction) ? EaseCurves.Evaluate(family, direction, t) : t,
        };
    }

    /// <summary>The family and direction a preset draws, or null for step, linear and the plain ease.</summary>
    public static (CurveFamily Family, CurveDirection Direction)? Shape(EasePreset preset) => preset switch
    {
        EasePreset.EaseIn => (CurveFamily.Quad, CurveDirection.In),
        EasePreset.EaseOut => (CurveFamily.Quad, CurveDirection.Out),
        _ => (uint)preset < (uint)Shapes.Length ? Shapes[(int)preset] : null,
    };

    /// <summary>The preset drawing this family in this direction.</summary>
    public static EasePreset Preset(CurveFamily family, CurveDirection direction) =>
        (family, direction) switch
        {
            (CurveFamily.Quad, CurveDirection.In) => EasePreset.EaseIn,
            (CurveFamily.Quad, CurveDirection.Out) => EasePreset.EaseOut,
            _ => Enum.Parse<EasePreset>($"{family}{direction}"),
        };

    /// <summary>Every preset's shape by value, so sampling never parses a name.</summary>
    private static readonly (CurveFamily, CurveDirection)?[] Shapes = BuildShapes();

    private static (CurveFamily, CurveDirection)?[] BuildShapes()
    {
        EasePreset[] values = Enum.GetValues<EasePreset>();
        var shapes = new (CurveFamily, CurveDirection)?[values.Length];
        foreach (EasePreset value in values)
            shapes[(int)value] = ParseShape(value.ToString());
        return shapes;
    }

    /// <summary>The family and direction a curve name ends in ("CubicInOut"), or null.</summary>
    public static (CurveFamily, CurveDirection)? ParseShape(string name)
    {
        foreach (CurveDirection direction in new[] { CurveDirection.InOut, CurveDirection.Out, CurveDirection.In })
        {
            string suffix = direction.ToString();
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal) &&
                Enum.TryParse(name[..^suffix.Length], out CurveFamily family))
            {
                return (family, direction);
            }
        }

        return null;
    }
}
