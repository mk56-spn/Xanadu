using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Tools.Animator;

/// <summary>
/// The FLOW tab: the clips the clip being edited may fade into, then the fade,
/// curve and pose gap of the selected one, and its preview controls.
/// </summary>
public static partial class AnimatorLayout
{
    public const float FlowRowHeight = 26f;
    public const float FlowToggleWidth = 44f;
    private const float FlowTitleHeight = 20f;
    private const float FlowTextHeight = 18f;
    private const float FlowControlHeight = 28f;
    private const float FlowStepWidth = 44f;

    /// <summary>Title, rule, fade, curve, gap and preview rows, and the spacing after each.</summary>
    private const float FlowFixedHeight =
        FlowTitleHeight + FlowTextHeight * 2 + FlowControlHeight * 3 + UiTheme.ContainerSpacing * 6;

    /// <summary>Width of one row in the clip list, inside its scrolling viewport.</summary>
    public const float FlowListWidth = ListWidth - 10;

    private static void BuildFlowBody(UiBuilder menu, Vector2 body, ref Refs refs)
    {
        refs.Ui.FlowTitle = menu.Text("", new Vector2(ListWidth, FlowTitleHeight), UiTheme.FontSize,
            UiTheme.Accent);

        // The clips this one may fade into: the flow system fills it with one
        // row per other clip, and it scrolls when there are more than fit.
        float viewportHeight = body.Y - UiTheme.ContainerPadding * 2 - FlowFixedHeight;
        menu.Container(Vector2.Zero, new Vector2(ListWidth, viewportHeight),
            StackDirection.Vertical, clip: true, color: Palette.SurfaceInset, takesInput: false);
        Entity viewport = menu.Current;
        refs.Ui.FlowList = List(menu, ListWidth);
        menu.End();
        viewport.AddComponent(new ScrollView(refs.Ui.FlowList));

        refs.Ui.FlowRule = menu.Text("", new Vector2(ListWidth, FlowTextHeight), UiTheme.SmallFontSize,
            UiTheme.Text, TextAlign.Left);

        menu.Container(Vector2.Zero, new Vector2(ButtonWidth, FlowControlHeight), spacing: 6, color: Clear);
        var step = new Vector2(FlowStepWidth, FlowControlHeight);
        FlowButton(menu, "-", ToolActionKind.TransitionFadeDown, step);
        refs.Ui.FlowFade = menu.Text("", new Vector2(ButtonWidth - FlowStepWidth * 2 - 12, FlowControlHeight),
            UiTheme.FontSize, UiTheme.Text);
        FlowButton(menu, "+", ToolActionKind.TransitionFadeUp, step);
        menu.End();

        menu.Container(Vector2.Zero, new Vector2(ButtonWidth, FlowControlHeight), spacing: 4, color: Clear);
        // The two long names get the room; IN and OUT share what is left.
        float word = (ButtonWidth - 12) * 0.3f;
        var wide = new Vector2(word, FlowControlHeight);
        var narrow = new Vector2((ButtonWidth - 12 - word * 2) / 2, FlowControlHeight);
        FlowButton(menu, "LINEAR", ToolActionKind.TransitionCurve, wide, nameof(XafFadeCurve.Linear));
        FlowButton(menu, "SMOOTH", ToolActionKind.TransitionCurve, wide, nameof(XafFadeCurve.Smooth));
        FlowButton(menu, "IN", ToolActionKind.TransitionCurve, narrow, nameof(XafFadeCurve.EaseIn));
        FlowButton(menu, "OUT", ToolActionKind.TransitionCurve, narrow, nameof(XafFadeCurve.EaseOut));
        menu.End();

        refs.Ui.FlowGap = menu.Text("", new Vector2(ListWidth, FlowTextHeight), UiTheme.SmallFontSize,
            UiTheme.TextDim, TextAlign.Left);

        menu.Container(Vector2.Zero, new Vector2(ButtonWidth, FlowControlHeight), spacing: 4, color: Clear);
        float small = (ButtonWidth - 8) / 4;
        FlowButton(menu, "PREVIEW", ToolActionKind.PreviewTransition,
            new Vector2(ButtonWidth - 8 - small * 2, FlowControlHeight));
        FlowButton(menu, "LOOP", ToolActionKind.ToggleFlowLoop, new Vector2(small, FlowControlHeight));
        FlowButton(menu, "SLOW", ToolActionKind.ToggleSlow, new Vector2(small, FlowControlHeight));
        menu.End();
    }

    private static Entity FlowButton(
        UiBuilder ui,
        string label,
        ToolActionKind kind,
        Vector2 size,
        string argument = "")
    {
        Entity button = ui.Button(label, size, fontSize: UiTheme.SmallFontSize);
        button.AddComponent(new ToolAction(kind, argument));
        AnimatorTooltips.Attach(button, kind, argument);
        return button;
    }
}
