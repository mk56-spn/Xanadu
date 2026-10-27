using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>Where the parts of a colour picker sit, relative to its saturation square.</summary>
public static class ColorPickerLayout
{
    public static readonly Vector2 Size = new(120, 120);
    public const float HueWidth = 16;
    public const float AlphaHeight = 14;
    public static readonly Vector2 HueOffset = new(Size.X / 2 + 18, 0);
    public static readonly Vector2 AlphaOffset = new(0, Size.Y / 2 + 16);
    public static readonly Vector2 SwatchSize = new(110, 44);
    public static readonly Vector2 SwatchOffset = new(Size.X / 2 + 18 + HueWidth / 2 + 14 + 55, -Size.Y / 2 + 22);
    public static readonly Vector2 HexOffset = SwatchOffset + new Vector2(0, 40);

    /// <summary>Vertical space a picker needs below its top edge.</summary>
    public const float Height = 120 + 30;

    public static Rect2 Square(Vector2 center) => new(center - Size / 2, Size);

    public static Rect2 HueBar(Vector2 center) =>
        new(center + HueOffset - new Vector2(HueWidth / 2, Size.Y / 2), new Vector2(HueWidth, Size.Y));

    public static Rect2 AlphaBar(Vector2 center) =>
        new(center + AlphaOffset - new Vector2(Size.X / 2, AlphaHeight / 2), new Vector2(Size.X, AlphaHeight));
}

/// <summary>
/// An HSV colour picker, carried by its saturation/value square. Hue and
/// alpha have their own bars. Hue is kept apart from the colour so it
/// survives passing through grey or black.
/// </summary>
public struct UiColorPicker : IComponent
{
    public Entity SvCursor;
    public Entity HueCursor;
    public Entity AlphaCursor;
    public Entity Alpha;
    public Entity Swatch;
    public Entity Hex;
    public float Hue;
    public float Saturation;
    public float Value;
    public float AlphaValue;

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
