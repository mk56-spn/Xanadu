using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Tools.Animator;

/// <summary>
/// Preview mode's pieces: the bar that takes the timeline's place, and the
/// colours the chrome turns so the mode reads at a glance.
/// </summary>
public static partial class AnimatorLayout
{
    /// <summary>As tall as the header, so preview mode is framed by two equal bars.</summary>
    public const float PreviewBarHeight = EditorShellMetrics.HeaderHeight;

    /// <summary>The header, status line and preview bar while previewing.</summary>
    public static readonly Color PreviewChrome = Palette.Violet.Darkened(0.65f);

    /// <summary>The viewport behind the rig while previewing: the editing backdrop, tinted.</summary>
    public static readonly Color PreviewBackdrop = Palette.SurfaceDeep.Lerp(Palette.Violet, 0.08f);

    /// <summary>Buttons on the preview bar; lit ones (loop, slow) use the selection colour.</summary>
    public static readonly Color PreviewButton = Palette.Violet.Darkened(0.25f);

    private const float PreviewCaptionWidth = 360f;
    private const float PreviewControl = EditorShellMetrics.HeaderControlHeight;

    /// <summary>
    /// Transport and readout at the left; what to preview and how at the right,
    /// then the way back to editing. The bar starts parked: it only joins the
    /// screen's stack in preview mode, in the timeline's place.
    /// </summary>
    private static void BuildPreviewBar(UiBuilder ui, Vector2 screen, ref Refs refs)
    {
        ui.Container(UiLayout.Hidden, new Vector2(screen.X - Margin * 2, PreviewBarHeight),
            color: PreviewChrome, takesInput: true);
        refs.Ui.PreviewBar = ui.Current;
        refs.Ui.PreviewBar.AddTag<LayoutIgnore>();

        float pad = (PreviewBarHeight - PreviewControl) / 2;
        EdgeGroup(ui, new Vector2(0, 0.5f), PreviewBarHeight, EditorShellMetrics.HeaderGap, pad);
        var transport = new Vector2(PreviewControl, PreviewControl);
        IconAction(ui, "PL", "play", ToolActionKind.Play, transport);
        IconAction(ui, "ST", "stop", ToolActionKind.Stop, transport);
        refs.Ui.PreviewCaption = ui.Text("", new Vector2(PreviewCaptionWidth, PreviewControl),
            UiTheme.FontSize, UiTheme.Text, TextAlign.Left);
        ui.End();

        EdgeGroup(ui, new Vector2(1, 0.5f), PreviewBarHeight, TimelineButtonGap, pad);
        PreviewAction(ui, "GAME", ToolActionKind.SequenceGame, 64);
        PreviewAction(ui, "ALL CLIPS", ToolActionKind.SequenceAll, 90);
        PreviewAction(ui, "FLOW", ToolActionKind.PreviewTransition, 64);
        PreviewAction(ui, "LOOP", ToolActionKind.ToggleLoop, 64);
        PreviewAction(ui, "SLOW", ToolActionKind.ToggleSlow, 64);
        PreviewAction(ui, "PATHS", ToolActionKind.ToggleMode, 64, nameof(AnimatorMode.MotionPaths));
        ui.RectEntity(Vector2.Zero, new Vector2(TimelineGroupGap, PreviewControl), Clear);
        PreviewAction(ui, "EDIT  (ESC)", ToolActionKind.ExitPreviewMode, 110);
        ui.End();
        ui.End();
    }

    private static Entity PreviewAction(UiBuilder ui, string label, ToolActionKind kind, float width, string argument = "")
    {
        Entity button = ui.Button(label, new Vector2(width, PreviewControl), PreviewButton,
            fontSize: UiTheme.SmallFontSize);
        button.AddComponent(new ToolAction(kind, argument));
        button.AddTag<AnimatorPreviewBarButton>();
        AnimatorTooltips.Attach(button, kind, argument);
        return button;
    }
}
