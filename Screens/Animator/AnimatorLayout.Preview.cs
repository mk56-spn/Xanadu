using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Animator;

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
        ui.Container(Vector2.Zero, new Vector2(screen.X - Margin * 2, PreviewBarHeight),
            color: PreviewChrome, takesInput: true);
        UiLayout.SetHidden(ui.Current, true);
        refs.Ui.PreviewBar = ui.Current;

        float pad = (PreviewBarHeight - PreviewControl) / 2;
        EdgeGroup(ui, new Vector2(0, 0.5f), PreviewBarHeight, EditorShellMetrics.HeaderGap, pad);
        var transport = new Vector2(PreviewControl, PreviewControl);
        IconAction(ui, "PL", "play", new StartPlaying(), transport);
        IconAction(ui, "ST", "stop", new StopPlaying(), transport);
        refs.Ui.PreviewCaption = ui.Text("", new Vector2(PreviewCaptionWidth, PreviewControl),
            UiTheme.FontSize, UiTheme.Text, TextAlign.Left);
        ui.End();

        EdgeGroup(ui, new Vector2(1, 0.5f), PreviewBarHeight, TimelineButtonGap, pad);
        PreviewAction(ui, "GAME", new PlayGameSequence(), 64);
        PreviewAction(ui, "ALL CLIPS", new PlayAllClips(), 90);
        PreviewAction(ui, "ATTACKS", new CycleAttacks(), 76);
        refs.Ui.MetronomeBpm = PreviewStepper(ui, 62, new StepMetronome(-1), new StepMetronome(1));
        refs.Ui.MetronomeEvery = PreviewStepper(ui, 74, new StepAttackEvery(-1), new StepAttackEvery(1));
        PreviewAction(ui, "FLOW", new PreviewTransition(), 64);
        PreviewAction(ui, "LOOP", new ToggleLoop(), 64);
        PreviewAction(ui, "SLOW", new ToggleSlow(), 64);
        // No way out here: the header's RIG | ANIMATE | PREVIEW switch (or Esc) leaves.
        PreviewAction(ui, "PATHS", new ToggleMode(AnimatorMode.MotionPaths), 64);
        ui.End();
        ui.End();
    }

    /// <summary>A value as text, then its - and + buttons (the shared stepper row); returns the text, which the preview system fills.</summary>
    private static Entity PreviewStepper(UiBuilder ui, float width, IIntent down, IIntent up)
    {
        float buttons = 2 * (UiNode.ControlRow * 1.5f + UiMetrics.Space.Sm);
        UiStepper parts = UiNode.Wrap(ui.Current, ui.Visuals).Stepper("", "", width + buttons)
            .Entity.GetComponent<UiStepper>();
        foreach ((Entity button, IIntent kind) in new[] { (parts.Less, down), (parts.More, up) })
        {
            button.AddComponent(new Intent(kind));
            button.AddTag<AnimatorPreviewBarButton>();
            AnimatorTooltips.Attach(button, kind);
        }

        return parts.Value;
    }

    private static Entity PreviewAction(UiBuilder ui, string label, IIntent action, float width)
    {
        Entity button = ui.Button(label, new Vector2(width, PreviewControl), PreviewButton,
            fontSize: UiTheme.SmallFontSize);
        button.AddComponent(new Intent(action));
        button.AddTag<AnimatorPreviewBarButton>();
        AnimatorTooltips.Attach(button, action);
        return button;
    }
}
