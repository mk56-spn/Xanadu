using Friflo.Engine.ECS;
using Xanadu.Core.Theme;

namespace Xanadu.Ui.Ecs.Ui.Tree;

/// <summary>
/// The shared slider row (<see cref="UiNode.Slider"/>): name, track, value. Its
/// node is the track, which carries <see cref="UiSlider"/>.
/// </summary>
public sealed class Slider(
    string label,
    float min,
    float max,
    float value,
    float width,
    string format = "0.00",
    float? rest = null,
    bool readout = true,
    string? tip = null)
    : UiSpec
{
    protected override UiNode Create(UiNode parent) =>
        parent.Slider(label, min, max, value, width, format, rest, readout, tip);
}

/// <summary>
/// The shared stepper row (<see cref="UiNode.Stepper"/>): name, value, - and +.
/// Its node is the row, which carries <see cref="UiStepper"/>; give the buttons
/// commands with <paramref name="less"/> and <paramref name="more"/>.
/// </summary>
public sealed class Stepper(
    string label,
    string value,
    float width,
    int type = UiType.Caption,
    UiCommand? less = null,
    UiCommand? more = null,
    string lessTip = "One less",
    string moreTip = "One more")
    : UiSpec
{
    /// <summary>The text showing the value, once built.</summary>
    public Entity Value => Node.Entity.GetComponent<UiStepper>().Value;

    protected override UiNode Create(UiNode parent)
    {
        UiNode row = parent.Stepper(label, value, width, type, lessTip, moreTip);
        UiStepper parts = row.Entity.GetComponent<UiStepper>();
        if (less is { } down)
            parts.Less.AddComponent(down);
        if (more is { } up)
            parts.More.AddComponent(up);
        return row;
    }
}

/// <summary>The shared on/off button (<see cref="UiNode.ToggleButton"/>); its node carries <see cref="Toggle"/>.</summary>
public sealed class ToggleButton(
    string on,
    string? off = null,
    bool value = false,
    int type = UiType.Caption,
    float? width = null,
    float grow = 0,
    string? tip = null)
    : UiSpec(width: width, grow: grow, tip: tip)
{
    protected override UiNode Create(UiNode parent) => parent.ToggleButton(on, off ?? on, value, type);
}

/// <summary>The shared pick (<see cref="UiNode.Choice"/>): a dropdown whose button reads what is picked. Its node is the button.</summary>
public sealed class Choice(
    System.Collections.Generic.IReadOnlyList<string> names,
    int selected,
    float width,
    int type = UiType.Caption,
    string? tip = null)
    : UiSpec(tip: tip)
{
    protected override UiNode Create(UiNode parent) => parent.Choice(names, selected, width, type);
}
