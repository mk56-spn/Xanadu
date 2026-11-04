using System;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator;

/// <summary>What the EASING window calls each curve, and how it describes it.</summary>
public static class AnimatorEasingNames
{
    /// <summary>The window's rows, gentlest first; each has an in, out and in-out curve.</summary>
    public static readonly XafCurveFamily[] Families = Enum.GetValues<XafCurveFamily>();

    public static readonly XafCurveDirection[] Directions = Enum.GetValues<XafCurveDirection>();

    /// <summary>The first row: the three curves that are not a family.</summary>
    public static readonly XafEasePreset[] Basics = { XafEasePreset.Step, XafEasePreset.Linear, XafEasePreset.Ease };

    public static string Label(XafEasePreset preset) => preset switch
    {
        XafEasePreset.Step => "STEP",
        XafEasePreset.Linear => "LINEAR",
        XafEasePreset.Ease => "SMOOTH",
        _ => XafEasing.Shape(preset) is var (family, direction)
            ? $"{Label(family)} {Label(direction)}"
            : preset.ToString().ToUpperInvariant(),
    };

    public static string Label(XafCurveFamily family) => family.ToString().ToUpperInvariant();

    public static string Label(XafCurveDirection direction) => direction switch
    {
        XafCurveDirection.In => "IN",
        XafCurveDirection.Out => "OUT",
        _ => "IN-OUT",
    };

    /// <summary>One line on how the curve moves, for the window and the tooltips.</summary>
    public static string Describe(XafEasePreset preset) => preset switch
    {
        XafEasePreset.Step => "Holds the value, then jumps at the next key",
        XafEasePreset.Linear => "Constant speed, no easing",
        XafEasePreset.Ease => "Gentle start and stop",
        _ => XafEasing.Shape(preset) is var (family, direction) ? Describe(family, direction) : "",
    };

    private static string Describe(XafCurveFamily family, XafCurveDirection direction) =>
        (family, direction) switch
        {
            (XafCurveFamily.Back, XafCurveDirection.In) => "Pulls back a little before it goes",
            (XafCurveFamily.Back, XafCurveDirection.Out) => "Overshoots a little, then settles",
            (XafCurveFamily.Back, _) => "Pulls back, then overshoots and settles",
            (XafCurveFamily.Elastic, XafCurveDirection.In) => "Winds up with a growing wobble",
            (XafCurveFamily.Elastic, XafCurveDirection.Out) => "Springs past the key and wobbles to rest",
            (XafCurveFamily.Elastic, _) => "Wobbles out of the start and into the end",
            (XafCurveFamily.Bounce, XafCurveDirection.In) => "Bounces up off the start",
            (XafCurveFamily.Bounce, XafCurveDirection.Out) => "Bounces to rest like a dropped ball",
            (XafCurveFamily.Bounce, _) => "Bounces at both ends",
            _ => $"{Strength(family)}, {Where(direction)}",
        };

    private static string Strength(XafCurveFamily family) => family switch
    {
        XafCurveFamily.Sine => "The softest ease",
        XafCurveFamily.Quad => "A gentle ease",
        XafCurveFamily.Cubic => "A firm ease",
        XafCurveFamily.Quart => "A strong ease",
        XafCurveFamily.Expo => "The sharpest ease",
        _ => "A sudden ease",
    };

    private static string Where(XafCurveDirection direction) => direction switch
    {
        XafCurveDirection.In => "slow to start",
        XafCurveDirection.Out => "slow to stop",
        _ => "slow at both ends",
    };

    /// <summary>The key that sets the curve, for the five that have one.</summary>
    public static string Shortcut(XafEasePreset preset) => preset switch
    {
        XafEasePreset.Step => " (1)",
        XafEasePreset.Linear => " (2)",
        XafEasePreset.Ease => " (3)",
        XafEasePreset.EaseIn => " (4)",
        XafEasePreset.EaseOut => " (5)",
        _ => "",
    };
}
