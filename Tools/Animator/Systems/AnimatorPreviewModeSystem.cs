using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Theme;
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
        Label(ui.PreviewButton, on ? "EXIT PREVIEW" : "PREVIEW");
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

        // The tab bodies float on the menu layer, so they are parked by hand.
        if (!ui.Tabs.IsNull && ui.Tabs.HasComponent<UiTabs>())
        {
            ref UiTabs tabs = ref ui.Tabs.GetComponent<UiTabs>();
            if (on)
            {
                mode.TabBodyCenter = tabs.BodyCenter;
                tabs.BodyCenter = UiLayout.Hidden;
            }
            else
            {
                tabs.BodyCenter = mode.TabBodyCenter;
            }
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
        Label(ui.PreviewCaption, caption);

        if (ui.PreviewBar.IsNull)
            return;

        foreach (Entity group in ui.PreviewBar.ChildEntities)
        {
            foreach (Entity button in group.ChildEntities)
                PaintToggle(button, clip?.Loop ?? false, slow, session.GetComponent<AnimatorModes>().MotionPaths);
        }
    }

    private static void PaintToggle(Entity button, bool loop, bool slow, bool paths)
    {
        if (!button.TryGetComponent(out ToolAction action))
            return;

        bool? lit = action.Kind switch
        {
            ToolActionKind.ToggleLoop => loop,
            ToolActionKind.ToggleSlow => slow,
            ToolActionKind.ToggleMode => paths,
            _ => null,
        };
        if (lit is { } on)
            Tint(button, on ? Lit : AnimatorLayout.PreviewButton);
    }

    private static void SetShown(Entity entity, bool shown)
    {
        if (entity.IsNull || shown == !entity.Tags.Has<LayoutIgnore>())
            return;

        if (!shown && entity.HasComponent<Position>())
        {
            entity.GetComponent<Position>().Value = UiLayout.Hidden;
            entity.AddTag<Moved>();
        }

        // Shown again, the parent's stack puts it back in its slot.
        UiLayout.SetIgnored(entity, !shown);
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

    private static void Label(Entity entity, string value)
    {
        if (entity.IsNull || !entity.HasComponent<Text>())
            return;

        ref Text text = ref entity.GetComponent<Text>();
        if (text.Value == value)
            return;

        text.Value = value;
        entity.AddTag<Dirty>();
    }
}
