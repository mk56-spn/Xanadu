namespace Xanadu.Theme;

/// <summary>
/// Control sizes and spacing, in design pixels at the 1920x1080 stage. Scenes
/// pick a step instead of inventing a number, which keeps a 4px rhythm and
/// makes a metrics change one edit here.
/// <para>
/// These are deliberately a small closed set. If a layout wants 37, it wants
/// 38 or 36 from <see cref="Control"/> plus padding from <see cref="Space"/>.
/// </para>
/// </summary>
public static class UiMetrics
{
    /// <summary>Heights of interactive controls and rows.</summary>
    public static class Control
    {
        /// <summary>Chips, tags, and inline fields.</summary>
        public const float Xs = 24;

        /// <summary>List rows, dropdown options, dense fields.</summary>
        public const float Sm = 30;

        /// <summary>The standard button.</summary>
        public const float Md = 38;

        /// <summary>A primary action or a dialog button.</summary>
        public const float Lg = 46;

        /// <summary>A menu button or a header control.</summary>
        public const float Xl = 56;
    }

    /// <summary>Gaps, paddings and margins on a 4px grid.</summary>
    public static class Space
    {
        /// <summary>Between an icon and its label.</summary>
        public const float Xxs = 2;

        /// <summary>Inside a tight chip.</summary>
        public const float Xs = 4;

        /// <summary>Between related controls.</summary>
        public const float Sm = 8;

        /// <summary>Panel padding and list gaps.</summary>
        public const float Md = 12;

        /// <summary>Between groups in a panel.</summary>
        public const float Lg = 16;

        /// <summary>Between panels and screen margins.</summary>
        public const float Xl = 24;

        /// <summary>Between major regions.</summary>
        public const float Xxl = 32;
    }

    /// <summary>Stroke widths.</summary>
    public static class Border
    {
        public const float Thin = 1;
        public const float Medium = 2;
        public const float Thick = 3;
    }

    /// <summary>Corner cuts and, later, corner radii.</summary>
    public static class Radius
    {
        public const float Sm = 4;
        public const float Md = 8;
        public const float Lg = 14;
    }

    /// <summary>The pitch of one list row: <see cref="Control.Sm"/> plus its gap.</summary>
    public const float RowPitch = 34;
}
