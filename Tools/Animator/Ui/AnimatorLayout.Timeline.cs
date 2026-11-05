using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator;

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
        EdgeGroup(ui, new Vector2(0, 0.5f), h, TimelineGroupGap, 0);
        ui.Container(Vector2.Zero, new Vector2(4, h), spacing: TimelineButtonGap,
            color: Clear, autoWidth: true);
        var transport = new Vector2(AnimatorTimelineModel.TransportButtonWidth, h);
        IconAction(ui, "PL", "play", ToolActionKind.Play, transport);
        IconAction(ui, "ST", "stop", ToolActionKind.Stop, transport);
        ui.End();
        refs.Ui.TimelineCaption = ui.Text("", new Vector2(TimelineCaptionWidth, h),
            UiTheme.SmallFontSize, UiTheme.TextDim, TextAlign.Left);
        ui.End();

        EdgeGroup(ui, new Vector2(1, 0.5f), h, TimelineGroupGap, 0);
        ui.Container(Vector2.Zero, new Vector2(4, h), spacing: TimelineButtonGap,
            color: Clear, autoWidth: true);
        refs.Ui.EasingButton = EasingButton(ui);
        ui.End();
        refs.Ui.ClipName = ClipNameField(ui);

        // Game preview and loop settings stay above the timeline, side by side,
        // because they change what the clip does rather than the clip itself.
        ui.Container(Vector2.Zero, new Vector2(4, h), spacing: TimelineButtonGap,
            color: Clear, autoWidth: true);
        HeaderButton(ui, "GAME", ToolActionKind.SequenceGame, 52);
        HeaderButton(ui, "ALL", ToolActionKind.SequenceAll, 40);
        ui.End();
        refs.Ui.LoopButton = HeaderButton(ui, "LOOP", ToolActionKind.ToggleLoop, 76);
        ui.End();
        ui.End();
        ui.End();
    }

    private static Entity ClipNameField(UiBuilder ui) =>
        TextInput(ui, new Vector2(150, AnimatorTimelineModel.HeaderHeight));
}
