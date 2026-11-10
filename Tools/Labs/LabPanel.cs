using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Labs;

/// <summary>
/// Rows for an experiment's side panel, and reading them back each frame:
/// steppers for discrete values (name, value, then - and +), sliders for
/// continuous ones, toggles, and buttons that report the frame they go down.
/// </summary>
internal sealed class LabPanel
{
    public const float Inner = 600;
    public const float RowHeight = 28;
    public const float LabelWidth = 130;
    private const float StepWidth = 46;
    private const float ReadoutWidth = 56;

    private readonly HashSet<long> _held = new();
    private readonly List<LabStepper> _steppers = new();

    public static UiNode Row(UiNode panel) =>
        panel.Row().Size(Inner, RowHeight).Gap(4).Cross(StackCrossAlignment.Center);

    /// <summary>
    /// A discrete value: its name, what it is now, then the step down and up
    /// buttons; <paramref name="spare"/> leaves room at the end of the row for another control.
    /// </summary>
    public LabStepper Stepper(UiNode panel, string label, Func<int> get, Action<int> set, int count,
        Func<int, string> name, float spare = 0)
    {
        UiNode row = Row(panel);
        row.Text(label, UiType.Caption, Palette.TextDim).Size(LabelWidth, RowHeight);
        Entity value = row.Text("", UiType.Small, Palette.Text).Size(Inner - LabelWidth - StepWidth * 2 - 12 - spare, RowHeight);
        Entity down = row.Button("-", UiType.Label).Size(StepWidth, RowHeight);
        Entity up = row.Button("+", UiType.Label).Size(StepWidth, RowHeight);
        var stepper = new LabStepper(row, value, down, up, get, set, count, name);
        _steppers.Add(stepper);
        return stepper;
    }

    /// <summary>A value set by dragging; a right-click puts back <paramref name="rest"/>.</summary>
    public static Entity Slider(UiNode panel, string label, float min, float max, float rest, string tooltip, string format = "0.00")
    {
        UiNode row = Row(panel);
        row.Text(label, UiType.Caption, Palette.TextDim).Size(LabelWidth, RowHeight);
        var size = new Vector2(Inner - LabelWidth - ReadoutWidth - 8, 10);
        UiNode track = row.Rect(Palette.Muted, size: size).Input();
        UiNode fill = row.Rect(Palette.PrimaryMuted, "", size).IgnoreLayout();
        UiNode readout = row.Text("", UiType.Caption, Palette.Text, TextAlign.Right).Size(ReadoutWidth, RowHeight);
        return track.With(new UiSlider(fill, readout, min, max, rest, format: format, defaultValue: rest))
            .Tooltip(tooltip + " · right-click resets");
    }

    public static Entity Toggle(UiNode row, string on, string off, bool value, float width, string tooltip) =>
        row.Button("", UiType.Caption).Size(width, RowHeight)
            .With(new Toggle(on, off, value))
            .Tooltip(tooltip);

    public static float Value(Entity slider) => slider.GetComponent<UiSlider>().Value;

    public static void SetValue(Entity slider, float value) => slider.GetComponent<UiSlider>().Value = value;

    public static bool On(Entity toggle) => toggle.GetComponent<Toggle>().Value;

    public static void SetOn(Entity toggle, bool on) => toggle.GetComponent<Toggle>().Value = on;

    /// <summary>Paints a row of choice buttons: bright blue for the chosen one only, muted for the rest.</summary>
    public static void ShowChoice(Entity[] buttons, int chosen)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            ref ButtonVisual visual = ref buttons[i].GetComponent<ButtonVisual>();
            visual.Normal = i == chosen ? Palette.Primary : Palette.Muted;
            visual.Hover = visual.Normal.Lerp(Colors.White, 0.16f);
            buttons[i].AddTag<Dirty>();
        }
    }

    /// <summary>True on the frame a button goes down.</summary>
    public bool Clicked(Entity button)
    {
        if (!button.IsNull && button.Tags.Has<Pressed>())
            return _held.Add(button.Id);

        _held.Remove(button.Id);
        return false;
    }

    /// <summary>Steps every stepper whose - or + went down this frame.</summary>
    public void PollSteppers()
    {
        foreach (LabStepper stepper in _steppers)
            stepper.Poll(this);
    }

    /// <summary>Writes every stepper's current value into its row.</summary>
    public void ShowSteppers()
    {
        foreach (LabStepper stepper in _steppers)
            stepper.Show();
    }
}

/// <summary>One stepper row; it reads and writes its value through the getter and setter it was made with.</summary>
internal sealed class LabStepper(
    UiNode row,
    Entity value,
    Entity down,
    Entity up,
    Func<int> get,
    Action<int> set,
    int count,
    Func<int, string> name)
{
    /// <summary>The stepper's row, for a control that shares it.</summary>
    public UiNode Row => row;

    public void Poll(LabPanel panel)
    {
        int step = (panel.Clicked(down) ? -1 : 0) + (panel.Clicked(up) ? 1 : 0);
        if (step != 0)
            set(((get() + step) % count + count) % count);
    }

    public void Show() => UiText.Set(value, name(get()));
}
