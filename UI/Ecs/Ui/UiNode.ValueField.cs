using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Ecs.Ui;

public readonly partial struct UiNode
{
    private const float SlotHeight = ControlRow;
    private const float SlotGap = 4;
    private const float ElementWidth = 12;

    /// <summary>One slot of a vector or matrix: room for a few digits, never stretched.</summary>
    private const float SlotWidth = 56;

    /// <summary>
    /// A value of any shape, top to bottom: its name in the accent colour, a
    /// colour's picker, then its slots, a row per matrix row. A vector's slots
    /// sit in one row, each after its letter (x y z w, r g b a); a matrix is a
    /// grid; a switch is a toggle per channel; a lone whole number has - and +
    /// either side. Slots are typed into, dragged sideways to scrub, and
    /// right-clicked to reset. Returns the field, which carries
    /// <see cref="UiValueField"/>.
    /// </summary>
    /// <param name="range">The span a slot's bar shows and a drag covers; guessed from the values when left out.</param>
    public UiNode ValueField(
        string label,
        UiValueShape shape,
        ReadOnlySpan<float> initial,
        float width,
        (float Min, float Max)? range = null,
        string? tooltip = null)
    {
        UiNode field = Column().Gap(SlotGap).Cross(StackCrossAlignment.Start).Width(width);
        if (label.Length > 0)
            field.Text(label, UiTheme.SmallFontSize, UiTheme.Accent).Size(width, 18);

        var values = new float[shape.Count];
        for (int i = 0; i < values.Length; i++)
            values[i] = shape.Clamp(i < initial.Length ? initial[i] : 0);
        (float min, float max) = range ?? UiValueFields.GuessRange(shape, values);

        var state = new UiValueField
        {
            Shape = shape,
            Values = values,
            Defaults = (float[])values.Clone(),
            Slots = new Entity[shape.Count],
            Min = min,
            Max = max,
        };

        if (shape.Colour)
        {
            state.Picker = field.Column().Width(width).Cross(StackCrossAlignment.Start)
                .ColorPicker(new Color(values[0], values[1], values[2], values[3]), readout: false)
                .Entity;
        }

        bool lone = shape.Count == 1 && shape.Scalar is UiScalar.Int or UiScalar.UInt;
        // A switch's toggle carries its own letter.
        bool lettered = !shape.IsMatrix && shape.Count > 1 && shape.Scalar != UiScalar.Bool;
        float steppers = lone ? 2 * (ControlRow * 1.5f + SlotGap) : 0;
        float letters = lettered ? ElementWidth + SlotGap : 0;
        // A lone number takes the row; vectors and matrices keep compact slots
        // rather than stretching to the field's width.
        float slotWidth = shape.Count == 1
            ? MathF.Max(24, width - steppers)
            : MathF.Min(SlotWidth, (width - SlotGap * (shape.Columns - 1) - letters * shape.Columns) / shape.Columns);

        for (int row = 0; row < shape.Rows; row++)
        {
            UiNode line = field.Row().Gap(SlotGap).Main(StackMainAlignment.Start);
            if (lone)
                state.Less = line.StepButton("-", "One less").Entity;

            for (int column = 0; column < shape.Columns; column++)
            {
                int index = row * shape.Columns + column;
                if (lettered)
                    line.Text(shape.Element(index), UiTheme.SmallFontSize, UiTheme.TextDim, TextAlign.Center).Size(ElementWidth, SlotHeight);
                state.Slots[index] = line.ValueSlot(field.Entity, index, shape, values[index], slotWidth, tooltip).Entity;
            }

            if (lone)
                state.More = line.StepButton("+", "One more").Entity;
        }

        field.Entity.AddComponent(state);
        return field;
    }

    /// <summary>A value field for one of <paramref name="material"/>'s values, shaped as the host reports it and bound to it: every edit lands on the material.</summary>
    public UiNode UniformField(CanvasMaterial material, MaterialParam param, float width, (float Min, float Max)? range = null)
    {
        UiValueShape shape = UiValueShape.Of(param);
        float[] current = shape.IsMatrix
            ? UiValueFields.FromMatrix(
                material.Matrices.TryGetValue(param.Name, out var matrix) ? matrix : System.Numerics.Matrix4x4.Identity, shape)
            : [.. Channels(material.Get(param.Name, param.Default))];
        return ValueField(param.Name, shape, current, width, range, param.Name)
            .With(new UiMaterialBinding(material, param.Name));

        static float[] Channels(Vector4 v) => [v.X, v.Y, v.Z, v.W];
    }

    /// <summary>
    /// A shader setting as every editor shows it: its name (FLARE BOX for
    /// <c>flare_box</c>), then a pick among its <c>[Options]</c> names
    /// (<see cref="Choice"/>, whose button this returns) or a value field of its
    /// shape (<see cref="ValueField"/>, which this returns) starting on
    /// <paramref name="stored"/>, else the declared default, which a right-click resets to.
    /// </summary>
    public UiNode SettingField(MaterialParam param, float[]? stored, float width, (float Min, float Max)? range = null)
    {
        UiValueShape shape = UiValueShape.Of(param);
        float[] defaults = UiValueFields.Defaults(param, shape);
        float[] initial = stored ?? defaults;
        string label = UiValueFields.SettingLabel(param.Name);
        if (param.Options is { Count: > 0 } options)
        {
            int selected = Math.Clamp((int)MathF.Round(initial.Length > 0 ? initial[0] : 0f), 0, options.Count - 1);
            UiNode column = Column().Gap(SlotGap).Cross(StackCrossAlignment.Start).Width(width);
            column.Text(label, UiTheme.SmallFontSize, UiTheme.Accent).Size(width, 18);
            return column.Choice(options, selected, width).Tooltip(param.Name);
        }

        UiNode node = ValueField(label, shape, initial, width, range, param.Name);
        ref UiValueField field = ref node.Entity.GetComponent<UiValueField>();
        Array.Copy(defaults, field.Defaults, Math.Min(defaults.Length, field.Defaults.Length));
        return node;
    }

    /// <summary>One number's slot: a toggle for a switch, else a numeric field with a bar along its foot for a float.</summary>
    private UiNode ValueSlot(Entity field, int index, UiValueShape shape, float value, float width, string? tooltip)
    {
        string name = shape.Element(index);
        string tip = tooltip is null ? name : name.Length == 0 ? tooltip : $"{tooltip}.{name}".Replace(".[", "[");
        var slot = new UiValueSlot(field, index) { ShownOn = value > 0.5f };
        if (shape.Scalar == UiScalar.Bool)
        {
            string on = name.Length == 0 ? "ON" : name.ToUpperInvariant();
            string off = name.Length == 0 ? "OFF" : name.ToUpperInvariant();
            UiNode toggle = Button(value > 0.5f ? on : off, UiTheme.SmallFontSize).Size(width, SlotHeight);
            toggle.Entity.AddComponent(new Toggle(on, off, value > 0.5f));
            return toggle.With(slot).Tooltip(tip.Length > 0 ? tip : "On or off");
        }

        UiNode box = Rect(Palette.SurfaceInset, "", new Vector2(width, SlotHeight))
            .Input()
            .With(new TextStyle(13, UiTheme.Text))
            .Align(TextAlign.Center)
            .With(new TextField("", numeric: true));
        if (shape.Scalar == UiScalar.Float)
        {
            UiNode bar = box.Rect(UiTheme.Accent, size: new Vector2(0, 2));
            bar.Entity.AddTag<LayoutIgnore>();
            bar.Entity.AddTag<UiRaised>();
            slot.Bar = bar.Entity;
        }

        string how = shape.IsWhole ? "drag sideways to step, type to set" : "drag sideways to scrub, type to set";
        return box.With(slot).Tooltip($"{(tip.Length > 0 ? tip + ": " : "")}{how}, right-click to reset");
    }
}
