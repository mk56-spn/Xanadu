using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

public readonly partial struct UiNode
{
    /// <summary>
    /// A colour picker over a row of label, swatch and hex field; returns the field,
    /// which carries <see cref="UiColorField"/>.
    /// </summary>
    public UiNode ColorField(string label, float width, string tooltip)
    {
        const float height = 28;
        const float gap = 6;
        const float labelWidth = 68;
        Entity picker = FieldPicker(width);
        UiNode row = Row().Size(width, height).Gap(gap).Main(StackMainAlignment.Start);
        row.Text(label, UiTheme.SmallFontSize, UiTheme.TextDim, TextAlign.Left).Size(labelWidth, height);
        UiNode swatch = row.Rect(Colors.White, size: new Vector2(height, height));
        return row.Rect(Palette.SurfaceInset, "", new Vector2(width - labelWidth - height - gap * 2, height))
            .Input()
            .With(new TextStyle(13, UiTheme.Text))
            .Align(TextAlign.Left)
            .With(new TextField(""))
            .With(new UiColorField(swatch.Entity) { Picker = picker })
            .Tooltip(tooltip);
    }

    /// <summary>
    /// A picker in a slot of its own at the left of a stack. The row below already
    /// shows the colour and its hex, so the picker's own swatch and hex stay hidden.
    /// </summary>
    private Entity FieldPicker(float width)
    {
        var slot = new Vector2(width, ColorPickerLayout.Height);
        UiBuilder builder = BeginChildren();
        builder.Container(Vector2.Zero, slot, StackDirection.Vertical, color: new Color(0, 0, 0, 0));
        bool real = builder.Visuals is GodotVisualRidFactory;
        List<Entity> parts = builder.ColorPicker(
            (ColorPickerLayout.Size - slot) / 2,
            Colors.White,
            real ? ColorPickerMaterials.Square() : null,
            real ? ColorPickerMaterials.Hue() : null);
        foreach (Entity part in parts)
            part.AddTag<LayoutIgnore>();
        builder.End();

        UiColorPicker picker = parts[0].GetComponent<UiColorPicker>();
        UiLayout.SetHidden(picker.Swatch, true);
        UiLayout.SetHidden(picker.Hex, true);
        return parts[0];
    }

    /// <summary>
    /// A colour picker with no alpha bar, its square centred on <paramref name="center"/>;
    /// returns the square, which carries <see cref="UiColorPicker"/>. It places its own
    /// parts, so it sits outside any stack.
    /// </summary>
    public UiNode OpaqueColorPicker(
        Vector2 center,
        Color initial,
        Material? squareMaterial = null,
        Material? hueMaterial = null)
    {
        UiBuilder builder = BeginChildren();
        List<Entity> parts = builder.ColorPicker(center, initial, squareMaterial, hueMaterial);
        foreach (Entity part in parts)
            part.AddTag<LayoutIgnore>();

        UiColorPicker picker = parts[0].GetComponent<UiColorPicker>();
        UiLayout.SetHidden(picker.Alpha, true);
        UiLayout.SetHidden(picker.AlphaCursor, true);
        return Leaf(parts[0]);
    }
}
