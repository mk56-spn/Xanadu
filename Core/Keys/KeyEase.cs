using Xanadu.Core.Timeline;

namespace Xanadu.Core.Keys;

/// <summary>
/// How a key's value travels to the next key. Past <see cref="CubicBezier"/>
/// each value names a fixed easing curve; its tangents are unused.
/// Quad in and out have no names of their own: the cubic form with tangents
/// draws them exactly. Stored as a byte, so values are only ever appended.
/// </summary>
public enum KeyEase
{
    Step,
    Linear,
    CubicBezier,
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

/// <summary>The named curves behind <see cref="KeyEase"/>.</summary>
public static class KeyEases
{
    /// <summary>The highest value a file may hold.</summary>
    public static readonly int Max = Enum.GetValues<KeyEase>().Length - 1;

    /// <summary>The family and direction of a named-curve ease, or null for step, linear and cubic.</summary>
    public static (CurveFamily Family, CurveDirection Direction)? Shape(KeyEase ease) =>
        (uint)ease < (uint)Shapes.Length ? Shapes[(int)ease] : null;

    /// <summary>Every ease's shape, by value, so sampling never parses a name.</summary>
    private static readonly (CurveFamily, CurveDirection)?[] Shapes = BuildShapes();

    private static (CurveFamily, CurveDirection)?[] BuildShapes()
    {
        KeyEase[] values = Enum.GetValues<KeyEase>();
        var shapes = new (CurveFamily, CurveDirection)?[values.Length];
        foreach (KeyEase value in values)
            shapes[(int)value] = EasePresets.ParseShape(value.ToString());
        return shapes;
    }
}
