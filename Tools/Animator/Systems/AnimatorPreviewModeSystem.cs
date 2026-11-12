using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Puts the shell in preview mode and back. Previewing hides the tool strip,
/// the right panel and the timeline, widens the viewport over the room they
/// leave, puts the preview bar in the timeline's place, turns the chrome
/// violet and draws only the art; leaving restores all of it.
/// </summary>
public sealed class AnimatorPreviewModeSystem : QuerySystem<AnimatorPreviewMode, AnimatorUi>
{
    private static readonly Color Lit = Palette.Selected;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            ref AnimatorPreviewMode mode = ref session.GetComponent<AnimatorPreviewMode>();
            AnimatorUi ui = session.GetComponent<AnimatorUi>();
            if (mode.On != mode.Applied)
            {
                Apply(session, ui, ref mode, mode.On);
                mode.Applied = mode.On;
            }

            if (mode.On)
                Present(session, ui);
        }
    }

    private static void Apply(Entity session, AnimatorUi ui, ref AnimatorPreviewMode mode, bool on)
    {
        EditorShellRects shell = EditorShell.Compute(
            ui.ScreenSize, new EditorShellOptions(EditorFooterHeight.Animator));
        Vector2 line = shell.StatusLine.Size;
        float width = ui.ScreenSize.X;
        float main = on
            ? ui.ScreenSize.Y - shell.Header.Size.Y - AnimatorLayout.PreviewBarHeight
            : shell.ToolStrip.Size.Y;
        Vector2 area = on ? new Vector2(width, main - line.Y) : shell.Viewport.Size;
        Vector2 status = new(area.X, line.Y);

        SetShown(ui.ToolStrip, !on);
        SetShown(ui.RightPanel, !on);
        SetShown(ui.Timeline, !on);
        SetShown(ui.PreviewBar, on);

        Resize(ui.MainRow, new Vector2(width, main));
        Resize(ui.ViewportColumn, new Vector2(area.X, main));
        Resize(ui.Preview, area);
        Resize(ui.StatusLine, status);

        Paint(ui.Header, on ? AnimatorLayout.PreviewChrome : UiTheme.Panel);
        Paint(ui.StatusLine, on ? AnimatorLayout.PreviewChrome : UiTheme.Panel);
        Paint(ui.Preview, on ? AnimatorLayout.PreviewBackdrop : Palette.SurfaceDeep);
        UiText.Set(ui.PreviewButton, on ? "EXIT PREVIEW" : "PREVIEW");
        Tint(ui.PreviewButton, on ? AnimatorLayout.PreviewButton : UiTheme.Button);

        // Only the art: the bone kites and handles are for editing.
        ref AnimatorModes modes = ref session.GetComponent<AnimatorModes>();
        if (on)
        {
            (mode.Kites, mode.Handles) = (modes.Kites, modes.Handles);
            (modes.Kites, modes.Handles) = (false, false);
        }
        else
        {
            (modes.Kites, modes.Handles) = (mode.Kites, mode.Handles);
        }
    }

    /// <summary>The bar's readout, and its loop and slow buttons lit while they are on.</summary>
    private static void Present(Entity session, AnimatorUi ui)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        XafClip? clip = AnimatorSession.CurrentClip(document);
        bool slow = session.GetComponent<AnimatorFlow>().Slow;
        string caption = clip is null
            ? "NO CLIP"
            : $"{clip.Id.ToUpperInvariant()}   {document.Runtime.CurrentTime:0.00} / {clip.Duration:0.00} s" +
              (slow ? "   ·   QUARTER SPEED" : "");
        AnimatorMetronome metronome = session.GetComponent<AnimatorMetronome>();
        AnimatorAttackCycle cycle = session.GetComponent<AnimatorAttackCycle>();
        if (cycle.Active)
            caption += "   ·   " + BeatDots(cycle.Beat, metronome.Every);
        UiText.Set(ui.PreviewCaption, caption);
        UiText.Set(ui.MetronomeBpm, $"{metronome.Bpm} BPM");
        UiText.Set(ui.MetronomeEvery, metronome.Every == 1 ? "EVERY BEAT" : $"EVERY {metronome.Every}");

        if (ui.PreviewBar.IsNull)
            return;

        bool attacks = session.GetComponent<AnimatorAttackCycle>().Active;
        foreach (Entity group in ui.PreviewBar.ChildEntities)
        {
            foreach (Entity button in group.ChildEntities)
                PaintToggle(button, clip?.Loop ?? false, slow, session.GetComponent<AnimatorModes>().MotionPaths, attacks);
        }
    }

    /// <summary>One dot a beat between attacks, the current one filled: ● ○ for an attack every two beats.</summary>
    private static string BeatDots(int beat, int every)
    {
        every = System.Math.Max(1, every);
        var dots = new System.Text.StringBuilder();
        for (int i = 0; i < every; i++)
            dots.Append(i == beat % every ? "● " : "○ ");
        return dots.ToString().TrimEnd();
    }

    private static void PaintToggle(Entity button, bool loop, bool slow, bool paths, bool attacks)
    {
        if (!button.TryGetToolAction(out ToolAction action))
            return;

        bool? lit = action.Kind switch
        {
            ToolActionKind.ToggleLoop => loop,
            ToolActionKind.ToggleSlow => slow,
            ToolActionKind.ToggleMode => paths,
            ToolActionKind.CycleAttacks => attacks,
            _ => null,
        };
        if (lit is { } on)
            Tint(button, on ? Lit : AnimatorLayout.PreviewButton);
    }

    private static void SetShown(Entity entity, bool shown)
    {
        UiLayout.SetHidden(entity, !shown);
    }

    private static void Resize(Entity entity, Vector2 size)
    {
        if (entity.IsNull || !entity.HasComponent<Rectangle>())
            return;

        entity.GetComponent<Rectangle>().Bounds = new Rect2(-size / 2, size);
        entity.AddTag<Dirty>();
        entity.AddTag<LayoutDirty>();
        if (!entity.Parent.IsNull && entity.Parent.HasComponent<UiContainer>())
            entity.Parent.AddTag<LayoutDirty>();
    }

    private static void Paint(Entity entity, Color color)
    {
        if (entity.IsNull || !entity.HasComponent<FillColor>())
            return;

        entity.GetComponent<FillColor>().Value = color;
        entity.AddTag<Dirty>();
    }

    private static void Tint(Entity button, Color color)
    {
        if (button.IsNull || !button.HasComponent<ButtonVisual>())
            return;

        ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
        if (visual.Normal == color)
            return;

        visual.Normal = color;
        visual.Hover = color.Lerp(Colors.White, 0.16f);
        button.AddTag<Dirty>();
    }
}
