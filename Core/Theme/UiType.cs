namespace Xanadu.Theme;

/// <summary>
/// The type scale. Sizes are design pixels at the 1920x1080 stage, so a scene
/// picks a role and never carries a number; retuning the type is one edit here.
/// <para>
/// Eight steps, because the screens had twenty-odd sizes. When a new size feels
/// necessary, check whether it is really one of these with different spacing.
/// </para>
/// </summary>
public static class UiType
{
    /// <summary>The screen title: "XANADU", "SONG TIMING".</summary>
    public const int Display = 52;

    /// <summary>A page title under the screen title.</summary>
    public const int Title = 40;

    /// <summary>A section header inside a panel.</summary>
    public const int Heading = 28;

    /// <summary>A card or sub-section header.</summary>
    public const int Section = 22;

    /// <summary>Button and control labels.</summary>
    public const int Label = 18;

    /// <summary>Body copy and field values.</summary>
    public const int Body = 16;

    /// <summary>Dense rows, hints, secondary metadata.</summary>
    public const int Small = 14;

    /// <summary>Footnotes, status lines and debug text.</summary>
    public const int Caption = 12;
}
