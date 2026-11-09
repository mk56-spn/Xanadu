using System;
using Xanadu.Timeline;

namespace Xanadu.Ecs;

/// <summary>What the ease picker, its hosts and their tooltips call each curve, and how they describe it.</summary>
public static class EaseNames
{
    /// <summary>The picker's rows, gentlest first; each has an in, out and in-out curve.</summary>
    public static readonly CurveFamily[] Families = Enum.GetValues<CurveFamily>();

    public static readonly CurveDirection[] Directions = Enum.GetValues<CurveDirection>();

    /// <summary>The first row: the three curves that are not a family.</summary>
    public static readonly EasePreset[] Basics = { EasePreset.Step, EasePreset.Linear, EasePreset.Ease };

    public static string Label(EasePreset preset) => preset switch
    {
        EasePreset.Step => "STEP",
        EasePreset.Linear => "LINEAR",
        EasePreset.Ease => "SMOOTH",
        _ => EasePresets.Shape(preset) is var (family, direction)
            ? $"{Label(family)} {Label(direction)}"
            : preset.ToString().ToUpperInvariant(),
    };

    /// <summary>A compact name for narrow buttons, such as the group panel's EASE cells: "LIN", "BACK OUT", "QUAD IO".</summary>
    public static string Short(EasePreset preset) => preset switch
    {
        EasePreset.Step => "STEP",
        EasePreset.Linear => "LIN",
        EasePreset.Ease => "SMOOTH",
        _ => EasePresets.Shape(preset) is var (family, direction)
            ? $"{Label(family)} {(direction == CurveDirection.InOut ? "IO" : Label(direction))}"
            : preset.ToString().ToUpperInvariant(),
    };

    public static string Label(CurveFamily family) => family.ToString().ToUpperInvariant();

    public static string Label(CurveDirection direction) => direction switch
    {
        CurveDirection.In => "IN",
        CurveDirection.Out => "OUT",
        _ => "IN-OUT",
    };

    /// <summary>One line on how the curve moves, for the window and the tooltips.</summary>
    public static string Describe(EasePreset preset) => preset switch
    {
        EasePreset.Step => "Holds the value, then jumps at the end",
        EasePreset.Linear => "Constant speed, no easing",
        EasePreset.Ease => "Gentle start and stop",
        _ => EasePresets.Shape(preset) is var (family, direction) ? Describe(family, direction) : "",
    };

    private static string Describe(CurveFamily family, CurveDirection direction) =>
        (family, direction) switch
        {
            (CurveFamily.Back, CurveDirection.In) => "Pulls back a little before it goes",
            (CurveFamily.Back, CurveDirection.Out) => "Overshoots a little, then settles",
            (CurveFamily.Back, _) => "Pulls back, then overshoots and settles",
            (CurveFamily.Elastic, CurveDirection.In) => "Winds up with a growing wobble",
            (CurveFamily.Elastic, CurveDirection.Out) => "Springs past the end and wobbles to rest",
            (CurveFamily.Elastic, _) => "Wobbles out of the start and into the end",
            (CurveFamily.Bounce, CurveDirection.In) => "Bounces up off the start",
            (CurveFamily.Bounce, CurveDirection.Out) => "Bounces to rest like a dropped ball",
            (CurveFamily.Bounce, _) => "Bounces at both ends",
            _ => $"{Strength(family)}, {Where(direction)}",
        };

    private static string Strength(CurveFamily family) => family switch
    {
        CurveFamily.Sine => "The softest ease",
        CurveFamily.Quad => "A gentle ease",
        CurveFamily.Cubic => "A firm ease",
        CurveFamily.Quart => "A strong ease",
        CurveFamily.Expo => "The sharpest ease",
        _ => "A sudden ease",
    };

    private static string Where(CurveDirection direction) => direction switch
    {
        CurveDirection.In => "slow to start",
        CurveDirection.Out => "slow to stop",
        _ => "slow at both ends",
    };
}
