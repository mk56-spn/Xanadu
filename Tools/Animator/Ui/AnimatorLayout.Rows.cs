using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator;

/// <summary>Panel rows shared by the tabs: subheadings inside a section, labelled steppers and sliders.</summary>
public static partial class AnimatorLayout
{
    private const float StepperGap = 6f;

    /// <summary>Names a group of rows inside a collapsible section.</summary>
    private static void Subhead(UiBuilder ui, string title) =>
        ui.Text(title, new Vector2(ButtonWidth, CaptionHeight), UiTheme.SmallFontSize,
            UiTheme.TextDim, TextAlign.Left);

    /// <summary>A value changed in steps: its name, then the step down and step up buttons.</summary>
    private static void Stepper(
        UiBuilder ui,
        string label,
        ToolActionKind down,
        ToolActionKind up,
        string downLabel = "-",
        string upLabel = "+")
    {
        StepperStart(ui, label);
        Action(ui, downLabel, down, StepperButton);
        Action(ui, upLabel, up, StepperButton);
        ui.End();
    }

    /// <summary>Opens a stepper's row and writes its name; the caller adds the two buttons and closes it.</summary>
    private static void StepperStart(UiBuilder ui, string label)
    {
        ui.Container(Vector2.Zero, new Vector2(ButtonWidth, RowHeight), spacing: StepperGap, color: Clear);
        ui.Text(label, new Vector2(StepperLabelWidth, RowHeight), UiTheme.SmallFontSize,
            UiTheme.Text, TextAlign.Left);
    }

    private static Vector2 StepperButton => new(RowHeight * 1.5f, RowHeight);

    /// <summary>What a stepper's name has left once its two buttons are placed.</summary>
    private static float StepperLabelWidth => ButtonWidth - (StepperButton.X + StepperGap) * 2;

    private const float SliderLabelWidth = 64f;
    private const float SliderReadoutWidth = 40f;
    private const float SliderTrackHeight = 10f;

    /// <summary>
    /// A value set by dragging: its name, the track, then the value as text.
    /// <see cref="Systems.AnimatorValueSliderSystem"/> keeps it on what it is
    /// bound to; a right-click puts it back to <paramref name="rest"/>.
    /// </summary>
    private static Entity SliderRow(
        UiBuilder ui,
        string label,
        AnimatorSliderValue value,
        float min,
        float max,
        float rest,
        string format,
        string tooltip,
        float width = ButtonWidth)
    {
        ui.Container(Vector2.Zero, new Vector2(width, RowHeight), spacing: StepperGap, color: Clear,
            cross: StackCrossAlignment.Center);
        ui.Text(label, new Vector2(SliderLabelWidth, RowHeight), UiTheme.SmallFontSize,
            UiTheme.Text, TextAlign.Left);

        var track = new Vector2(width - SliderLabelWidth - SliderReadoutWidth - StepperGap * 2, SliderTrackHeight);
        Entity slider = ui.RectEntity(Vector2.Zero, track, Palette.Muted, takesInput: true);
        Entity fill = ui.RectEntity(Vector2.Zero, track, Palette.PrimaryMuted, "");
        fill.AddTag<LayoutIgnore>();

        Entity readout = ui.Text("", new Vector2(SliderReadoutWidth, RowHeight), UiTheme.SmallFontSize,
            UiTheme.Text, TextAlign.Right);
        ui.End();

        slider.AddComponent(new UiSlider(fill, readout, min, max, rest, format: format, defaultValue: rest));
        slider.AddComponent(new AnimatorValueSlider(value, readout));
        slider.AddComponent(new UiTooltip(tooltip + " · right-click resets"));
        return slider;
    }
}
