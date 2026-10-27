using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

public sealed partial class UiBuilder
{
    /// <summary>
    /// Creates a colour picker whose saturation square is centred on
    /// <paramref name="center"/>. Returns every entity it made, the square,
    /// which carries <see cref="UiColorPicker"/>, first. The materials draw
    /// the square and hue bar; without them both stay plain white.
    /// </summary>
    public List<Entity> ColorPicker(
        Vector2 center,
        Color initial,
        Material? squareMaterial = null,
        Material? hueMaterial = null)
    {
        Entity square = RectEntity(center, ColorPickerLayout.Size, Colors.White);
        if (squareMaterial is not null)
            square.AddComponent(new UiMaterial(squareMaterial));

        Entity hueBar = RectEntity(
            center + ColorPickerLayout.HueOffset,
            new Vector2(ColorPickerLayout.HueWidth, ColorPickerLayout.Size.Y),
            Colors.White);
        if (hueMaterial is not null)
            hueBar.AddComponent(new UiMaterial(hueMaterial));

        Entity alpha = LinearGradientEntity(
            center + ColorPickerLayout.AlphaOffset,
            new Vector2(ColorPickerLayout.Size.X, ColorPickerLayout.AlphaHeight),
            new LinearGradient(new Color(initial, 0), new Color(initial, 1)));
        Entity swatch = RectEntity(
            center + ColorPickerLayout.SwatchOffset,
            ColorPickerLayout.SwatchSize,
            initial);
        Entity hex = Text(
            "",
            new Vector2(ColorPickerLayout.SwatchSize.X + 20, 24),
            14,
            Colors.White,
            TextAlign.Center,
            center + ColorPickerLayout.HexOffset);

        Entity svCursor = Cursor(
            [new(-5, -5), new(5, -5), new(5, 5), new(-5, 5)],
            closed: true);
        Entity hueCursor = Cursor(
            [new(-ColorPickerLayout.HueWidth / 2 - 3, 0), new(ColorPickerLayout.HueWidth / 2 + 3, 0)],
            closed: false);
        Entity alphaCursor = Cursor(
            [new(0, -ColorPickerLayout.AlphaHeight / 2 - 3), new(0, ColorPickerLayout.AlphaHeight / 2 + 3)],
            closed: false);

        var picker = new UiColorPicker
        {
            SvCursor = svCursor,
            HueCursor = hueCursor,
            AlphaCursor = alphaCursor,
            Alpha = alpha,
            Swatch = swatch,
            Hex = hex,
        };
        picker.Color = initial;
        square.AddComponent(picker);

        return [square, hueBar, alpha, swatch, hex, svCursor, hueCursor, alphaCursor];
    }

    private Entity Cursor(Vector2[] points, bool closed)
    {
        Entity cursor = ShapeEntity(points, default, Colors.White, 2, closed);
        cursor.AddTag<UiRaised>();
        return cursor;
    }
}
