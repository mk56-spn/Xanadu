using System.Collections.Generic;

namespace Xanadu.Core.Maths;

/// <summary>
/// This class contains color constants created from standardized color names.
/// The standardized color set is based on the X11 and .NET color names.
/// </summary>
public static class Colors
{
    // Color names and values are derived from core/math/color_names.inc
#pragma warning disable CS1591 // Disable warning: "Missing XML comment for publicly visible type or member"
    public static Color Black => new Color(0x000000FF);
    public static Color Blue => new Color(0x0000FFFF);
    public static Color Crimson => new Color(0xDC143CFF);
    public static Color Cyan => new Color(0x00FFFFFF);
    public static Color DarkOrange => new Color(0xFF8C00FF);
    public static Color DarkRed => new Color(0x8B0000FF);
    public static Color Gold => new Color(0xFFD700FF);
    public static Color Gray => new Color(0xBEBEBEFF);
    public static Color Green => new Color(0x00FF00FF);
    public static Color GreenYellow => new Color(0xADFF2FFF);
    public static Color Lime => new Color(0x00FF00FF);
    public static Color MediumPurple => new Color(0x9370DBFF);
    public static Color Orange => new Color(0xFFA500FF);
    public static Color Pink => new Color(0xFFC0CBFF);
    public static Color Red => new Color(0xFF0000FF);
    public static Color Tan => new Color(0xD2B48CFF);
    public static Color Teal => new Color(0x008080FF);
    public static Color Transparent => new Color(0xFFFFFF00);
    public static Color Violet => new Color(0xEE82EEFF);
    public static Color White => new Color(0xFFFFFFFF);
    public static Color Yellow => new Color(0xFFFF00FF);
#pragma warning restore CS1591
}
