using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Ui.Ecs.Ui;

public readonly partial struct UiNode
{
    /// <summary>
    /// A colour setting, top to bottom: its name in the accent colour, the
    /// picker, then the swatch and hex field. Returns the field, which carries
    /// <see cref="UiColorField"/>.
    /// </summary>
    public UiNode ColorField(string label, float width, string tooltip)
    {
        const float height = 28;
        const float gap = 6;
        Text(label, UiTheme.SmallFontSize, UiTheme.Accent).Size(width, 18);
        Entity picker = FieldPicker(width);
        UiNode row = Row().Size(width, height).Gap(gap).Main(StackMainAlignment.Start);
        // The swatch can be dragged onto another colour, and either half of the
        // field takes one dropped on it.
        UiNode swatch = row.Rect(Colors.White, size: new Vector2(height, height))
            .Input()
            .Tag<UiColorDrop>()
            .Tooltip("Drag this colour onto another to copy it there");
        swatch.Entity.AddTag<UiColorSwatch>();
        UiNode field = row.Rect(Palette.SurfaceInset, "", new Vector2(width - height - gap, height))
            .Input()
            .Tag<UiColorDrop>()
            .With(new TextStyle(13, UiTheme.Text))
            .Align(TextAlign.Left)
            .With(new TextField(""))
            .With(new UiColorField(swatch.Entity) { Picker = picker })
            .Tooltip(tooltip);

        // A colour dragged in, onto either half, lands like a picked one.
        Entity entity = field.Entity;
        entity.OnColorDropped(color => UiColorFields.Drop(entity, color));
        swatch.Entity.OnColorDropped(color => UiColorFields.Drop(entity, color));
        return field;
    }

    /// <summary>
    /// A picker at the left of a slot as wide as the field. The row below already
    /// shows the colour and its hex, so the picker has no readout of its own.
    /// </summary>
    private Entity FieldPicker(float width) =>
        Column().Width(width).Cross(StackCrossAlignment.Start)
            .ColorPicker(Colors.White, readout: false)
            .Entity;

    /// <summary>
    /// A colour picker laid out as a stack: the saturation/value square with the
    /// alpha bar under it, the hue bar beside them, then the swatch and hex
    /// readout. Returns the stack, which carries <see cref="UiColorPicker"/>.
    /// </summary>
    public UiNode ColorPicker(Color initial, bool alpha = true, bool readout = true)
    {
        bool real = BeginChildren().Visuals.LoadsAssets;
        UiNode picker = Row().Gap(ColorPickerLayout.Gap).Cross(StackCrossAlignment.Start);
        UiNode left = picker.Column().Gap(ColorPickerLayout.Gap);

        UiNode square = left.Rect(Colors.White, size: ColorPickerLayout.Size);
        if (real)
            square.With(new UiMaterial(ColorPickerMaterials.Square()));
        UiNode hueBar = picker.Rect(Colors.White, size: new Vector2(ColorPickerLayout.HueWidth, ColorPickerLayout.Side));
        if (real)
            hueBar.With(new UiMaterial(ColorPickerMaterials.Hue()));

        var state = new UiColorPicker
        {
            Square = square.Entity,
            HueBar = hueBar.Entity,
            SvCursor = square.Cursor([new(-5, -5), new(5, -5), new(5, 5), new(-5, 5)], closed: true),
            HueCursor = hueBar.Cursor(
                [new(-ColorPickerLayout.HueWidth / 2 - 3, 0), new(ColorPickerLayout.HueWidth / 2 + 3, 0)],
                closed: false),
        };

        if (alpha)
        {
            UiBuilder builder = left.BeginChildren();
            Entity bar = builder.LinearGradientEntity(
                Vector2.Zero,
                new Vector2(ColorPickerLayout.Side, ColorPickerLayout.AlphaHeight),
                new LinearGradient(new Color(initial, 0), new Color(initial)));
            state.Alpha = bar;
            state.AlphaCursor = left.Leaf(bar).Cursor(
                [new(0, -ColorPickerLayout.AlphaHeight / 2 - 3), new(0, ColorPickerLayout.AlphaHeight / 2 + 3)],
                closed: false);
        }

        if (readout)
        {
            UiNode column = picker.Column().Gap(UiMetrics.Space.Xs);
            state.Swatch = column.Rect(initial, size: ColorPickerLayout.SwatchSize).Entity;
            state.Hex = column.Text("", 14, Colors.White, TextAlign.Center)
                .Size(ColorPickerLayout.SwatchSize.X + 20, 24)
                .Entity;
        }

        state.Color = initial;
        picker.Entity.AddComponent(state);
        return picker;
    }

    /// <summary>A cursor drawn over this bar, in the bar's own space, outside layout.</summary>
    private Entity Cursor(Vector2[] points, bool closed)
    {
        Entity cursor = Shape(points, default, Colors.White, 2, closed).Entity;
        cursor.AddTag<UiRaised>();
        cursor.AddTag<LayoutIgnore>();
        return cursor;
    }
}
