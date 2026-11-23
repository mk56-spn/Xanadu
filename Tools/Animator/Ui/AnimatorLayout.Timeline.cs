using Xanadu.Core.Theme;
using Friflo.Engine.ECS;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Ui;

/// <summary>The strip above the timeline's rows: transport, caption and the clip's controls.</summary>
public static partial class AnimatorLayout
{
    private static void BuildTimeline(UiBuilder ui, Vector2 screen, ref Refs refs)
    {
        float width = screen.X - Margin * 2;
        ui.Container(Vector2.Zero, new Vector2(width, BottomHeight),
            StackDirection.Vertical, color: UiTheme.Panel, padding: 10,
            takesInput: true, main: StackMainAlignment.Start);
        refs.Ui.Timeline = ui.Current;

        // Transport and the caption at the left; the clip's controls at the right,
        // in groups: interpolation, name, preview, loop.
        float h = AnimatorTimelineModel.HeaderHeight;
        ui.Container(Vector2.Zero, new Vector2(width - 20, h), color: Clear);
        refs.Ui.TimelineHeader = ui.Current;
        EdgeGroup(ui, new Vector2(0, 0.5f), h, TimelineGroupGap, 0);
        ui.Container(Vector2.Zero, new Vector2(4, h), spacing: TimelineButtonGap,
            color: Clear, autoWidth: true);
        var transport = new Vector2(AnimatorTimelineModel.TransportButtonWidth, h);
        IconAction(ui, "PL", "play", ToolActionKind.Play, transport);
        IconAction(ui, "ST", "stop", ToolActionKind.Stop, transport);
        Entity autoKey = ui.Button("AUTO KEY", new Vector2(84, h), fontSize: UiTheme.SmallFontSize);
        autoKey.AddComponent(new Toggle("AUTO KEY", "AUTO KEY", false, Palette.Warning));
        autoKey.AddComponent(new ModeToggle(AnimatorMode.AutoKey));
        AnimatorTooltips.Attach(autoKey, ToolActionKind.ToggleMode, nameof(AnimatorMode.AutoKey));
        ui.End();
        refs.Ui.TimelineCaption = ui.Text("", new Vector2(TimelineCaptionWidth, h),
            UiTheme.SmallFontSize, UiTheme.TextDim, TextAlign.Left);
        ui.End();

        EdgeGroup(ui, new Vector2(1, 0.5f), h, TimelineGroupGap, 0);
        ui.Container(Vector2.Zero, new Vector2(4, h), spacing: TimelineButtonGap,
            color: Clear, autoWidth: true);
        refs.Ui.EasingButton = EasingButton(ui);
        Entity full = ui.Button("SUMMARY", new Vector2(84, h), fontSize: UiTheme.SmallFontSize);
        full.AddComponent(new Toggle("FULL", "SUMMARY", false));
        full.AddComponent(new ModeToggle(AnimatorMode.FullKeys));
        AnimatorTooltips.Attach(full, ToolActionKind.ToggleMode, nameof(AnimatorMode.FullKeys));
        ui.End();
        refs.Ui.ClipName = ClipNameField(ui);

        // Loop stays above the timeline: it changes what the clip does. GAME
        // and ALL live on the preview bar only.
        refs.Ui.LoopButton = HeaderButton(ui, "LOOP", ToolActionKind.ToggleLoop, 76);
        ui.End();
        ui.End();
        ui.End();
    }

    private static Entity ClipNameField(UiBuilder ui) =>
        TextInput(ui, new Vector2(150, AnimatorTimelineModel.HeaderHeight));
}
