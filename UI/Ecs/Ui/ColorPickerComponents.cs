using Friflo.Engine.ECS;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>The sizes of a colour picker's parts; a stack places them.</summary>
public static class ColorPickerLayout
{
    /// <summary>The square's side; the bars are sized to it.</summary>
    public const float Side = 84;

    public static readonly Vector2 Size = new(Side, Side);
    public const float HueWidth = 12;
    public const float AlphaHeight = 10;
    public static readonly Vector2 SwatchSize = new(110, 44);

    /// <summary>Between the square, its bars and the readout.</summary>
    public const float Gap = 12;
}

/// <summary>
/// An HSV colour picker, carried by the stack holding its parts: the
/// saturation/value square, and the hue and alpha bars. Each cursor is a child
/// of its bar, placed in the bar's own space. Hue is kept apart from the colour so it
/// survives passing through grey or black.
/// </summary>
public struct UiColorPicker : IComponent
{
    public Entity Square;
    public Entity HueBar;
    public Entity SvCursor;
    public Entity HueCursor;

    /// <summary>The alpha bar and its cursor; null in a picker built without alpha.</summary>
    public Entity AlphaCursor;
    public Entity Alpha;

    /// <summary>The swatch and hex readout; null in a picker built without them.</summary>
    public Entity Swatch;
    public Entity Hex;
    public float Hue;
    public float Saturation;
    public float Value;
    public float AlphaValue;

    /// <summary>The pointer is holding one of its parts; the system that moves it keeps this current.</summary>
    public bool Dragging;

    public Color Color
    {
        readonly get => Color.FromHsv(Hue, Saturation, Value, AlphaValue);
        set
        {
            // Grey and black carry no hue, so keep the one already chosen.
            if (value.S > 0 && value.V > 0)
                Hue = value.H;

            Saturation = value.S;
            Value = value.V;
            AlphaValue = value.A;
        }
    }
}
