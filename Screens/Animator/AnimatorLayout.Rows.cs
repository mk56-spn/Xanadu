using Friflo.Engine.ECS;
using Xanadu.Model.Animator;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Animator;

/// <summary>Panel rows shared by the tabs: subheadings inside a section, labelled steppers and sliders.</summary>
public static partial class AnimatorLayout
{
    /// <summary>Names a group of rows inside a collapsible section.</summary>
    private static void Subhead(UiBuilder ui, string title) =>
        ui.Text(title, new Vector2(ButtonWidth, CaptionHeight), UiTheme.SmallFontSize,
            UiTheme.TextDim, TextAlign.Left);

    /// <summary>A value changed in steps, as the shared stepper row: its name, then the step down and step up buttons.</summary>
    private static void Stepper(
        UiBuilder ui,
        string label,
        IIntent down,
        IIntent up,
        string downLabel = "-",
        string upLabel = "+")
    {
        UiStepper parts = UiNode.Wrap(ui.Current, ui.Visuals).Stepper(label, "", ButtonWidth).Entity.GetComponent<UiStepper>();
        Bind(parts.Less, down, downLabel);
        Bind(parts.More, up, upLabel);
    }

    /// <summary>Gives a shared step button the animator action it runs, and its sign when it isn't - or +.</summary>
    private static void Bind(Entity button, IIntent action, string sign)
    {
        button.AddComponent(new Intent(action));
        AnimatorTooltips.Attach(button, action);
        if (button.GetComponent<Text>().Value != sign)
            UiText.Set(button, sign);
    }

    /// <summary>
    /// A number set by scrubbing or typing: a value field (label, then a slot
    /// with its bar) bound to <paramref name="value"/>.
    /// <see cref="AnimatorValueFieldSystem"/> keeps it on the rig; a
    /// right-click puts it back to <paramref name="rest"/>.
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
        float width = ButtonWidth) =>
        Field(ui, label, AnimatorValueBinding.Of(value), UiValueShape.Float, [rest], (min, max), tooltip, width);

    /// <summary>
    /// A value field of any shape bound to what it edits in the rig, added to
    /// the builder's current container.
    /// </summary>
    private static Entity Field(
        UiBuilder ui,
        string label,
        AnimatorValueBinding binding,
        UiValueShape shape,
        float[] rest,
        (float Min, float Max)? range,
        string tooltip,
        float width)
    {
        UiNode field = UiNode.Wrap(ui.Current, ui.Visuals).ValueField(label, shape, rest, width, range, tooltip);
        field.Entity.AddComponent(binding);
        return field.Entity;
    }
}
