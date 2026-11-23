using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Theme;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// The shared control rows: one slider, one stepper, one toggle for every
/// screen and tool, so the same idea never gets built twice. Each row is as
/// tall as a value field's slot and lays out the same way wherever it sits:
/// its name in the first <see cref="ControlLabelShare"/> of the row, the control
/// after it.
/// </summary>
public readonly partial struct UiNode
{
    /// <summary>How tall every control row is: a value field's slot, a slider, a stepper.</summary>
    public const float ControlRow = UiMetrics.Control.Xs;

    /// <summary>The part of a control row its name takes.</summary>
    public const float ControlLabelShare = 0.32f;

    /// <summary>How thick a slider's track is.</summary>
    public const float SliderTrack = 8;

    /// <summary>How wide a slider's readout is: room for a few digits.</summary>
    public const float SliderReadout = 48;

    /// <summary>
    /// A number set by dragging along a track: its name (when given), the track,
    /// then the value (when <paramref name="readout"/>). A right-click on the track
    /// puts back <paramref name="rest"/> (the value it starts on when left out).
    /// Returns the track, which carries <see cref="UiSlider"/>: bind and read it there.
    /// </summary>
    public UiNode Slider(
        string label,
        float min,
        float max,
        float value,
        float width,
        string format = "0.00",
        float? rest = null,
        bool readout = true,
        string? tooltip = null)
    {
        UiNode row = ControlLine(label, width);
        UiNode track = row.Rect(row.Roles.Secondary.Fill, size: new Vector2(0, SliderTrack)).Input().Grow();

        // The slider system lays its fill over the track each frame.
        UiNode fill = row.Rect(row.Roles.Primary.Fill, "", new Vector2(0, SliderTrack)).IgnoreLayout();
        Entity number = readout
            ? row.Text("", UiType.Caption, row.Roles.Text, TextAlign.Right).Size(SliderReadout, ControlRow).Entity
            : default;

        float back = rest ?? value;
        track.With(new UiSlider(fill.Entity, number, min, max, value, format: format, defaultValue: back));
        return tooltip is null ? track.Tooltip("Right-click resets") : track.Tooltip(tooltip + " · right-click resets");
    }

    /// <summary>
    /// A value changed in steps: its name, what it is now, then - and +. Returns
    /// the row, which carries <see cref="UiStepper"/> with its value text and its
    /// two buttons; whoever owns the value reads their presses and sets the text.
    /// Give the buttons commands with <see cref="UiStepper.Less"/> and
    /// <see cref="UiStepper.More"/> (<c>.With(UiCommand.Of(...))</c>).
    /// </summary>
    public UiNode Stepper(
        string label,
        string value,
        float width,
        int type = UiType.Caption,
        string lessTip = "One less",
        string moreTip = "One more")
    {
        UiNode row = ControlLine(label, width, type);
        UiNode shown = row.Text(value, type, row.Roles.Text).Height(ControlHeight(type)).Grow().Fit();
        UiNode less = row.StepButton("-", lessTip, type);
        UiNode more = row.StepButton("+", moreTip, type);
        return row.With(new UiStepper(shown.Entity, less.Entity, more.Entity));
    }

    /// <summary>A square - or + button, as every stepper and a lone whole number's value field have.</summary>
    public UiNode StepButton(string sign, string tooltip, int type = UiType.Caption) =>
        Button(sign, type).Size(ControlHeight(type) * 1.5f, ControlHeight(type)).Tooltip(tooltip);

    /// <summary>
    /// How tall a control row in <paramref name="type"/>'s size is:
    /// <see cref="ControlRow"/> for the small sizes tools use, taller for the
    /// big type a player-facing screen sets its controls in.
    /// </summary>
    public static float ControlHeight(int type) => MathF.Max(ControlRow, MathF.Ceiling(type * 1.6f));

    /// <summary>
    /// A button that is on or off: bright while on, muted while off, and its
    /// label follows (<paramref name="on"/> / <paramref name="off"/>; give one
    /// label for both when only the colour should change). Carries
    /// <see cref="Toggle"/>; listen with <see cref="UiToggles.OnToggled"/>.
    /// </summary>
    public UiNode ToggleButton(string on, string off, bool value, int type = UiType.Caption) =>
        Button(value ? on : off, type)
            .Height(ControlHeight(type))
            .With(new Toggle(on, off, value, Roles.Chosen.Fill, Roles.Secondary.Fill));

    /// <summary>
    /// A pick of one of <paramref name="names"/>: a dropdown whose button reads
    /// what is picked (<see cref="DropdownShowsPick"/>). Read and set the pick on
    /// its <see cref="Dropdown"/>; a pick from the list also adds
    /// <see cref="DropdownChanged"/>. Returns the button.
    /// </summary>
    public UiNode Choice(IReadOnlyList<string> names, int selected, float width, int type = UiType.Caption)
    {
        var picks = new DropdownShowsPick(names);
        UiNode button = Dropdown(picks.Text(selected), names, selected, new Vector2(width, ControlHeight(type))).Button;
        return button.With(picks);
    }

    /// <summary>A control row: <paramref name="width"/> wide, its name first when it has one.</summary>
    private UiNode ControlLine(string label, float width, int type = UiType.Caption)
    {
        float height = ControlHeight(type);
        UiNode row = Row().Size(width, height).Gap(UiMetrics.Space.Sm).Cross(StackCrossAlignment.Center);
        if (label.Length > 0)
            row.Text(label, type).DimText().Size(width * ControlLabelShare, height);
        return row;
    }
}

/// <summary>A stepper row's parts (<see cref="UiNode.Stepper"/>): the value it shows and its - and + buttons.</summary>
public struct UiStepper(Entity value, Entity less, Entity more) : IComponent
{
    public Entity Value = value;
    public Entity Less = less;
    public Entity More = more;
}
