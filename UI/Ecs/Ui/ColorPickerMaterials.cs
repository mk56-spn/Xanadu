using Xanadu.Systems.Canvas;

namespace Xanadu.Ecs;

/// <summary>The materials a colour picker's square and hue bar draw with.</summary>
public static class ColorPickerMaterials
{
    public const string SquareShader = "Assets/Shaders/Ui/colour_square.slang";
    public const string HueShader = "Assets/Shaders/Ui/colour_hue.slang";

    /// <summary>Saturation left to right by value bottom to top, at the <c>hue</c> value.</summary>
    public static CanvasMaterial Square() => new(SquareShader);

    /// <summary>Hue from top, red, to bottom, red again.</summary>
    public static CanvasMaterial Hue() => new(HueShader);
}
