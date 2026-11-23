using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Animator;

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
        IconAction(ui, "PL", "play", new StartPlaying(), transport);
        IconAction(ui, "ST", "stop", new StopPlaying(), transport);
        Entity autoKey = ui.Button("AUTO KEY", new Vector2(84, h), fontSize: UiTheme.SmallFontSize);
        autoKey.AddComponent(new Toggle("AUTO KEY", "AUTO KEY", false, Palette.Warning));
        autoKey.AddComponent(new Switches(AnimatorModeTags.Of(AnimatorMode.AutoKey)));
        AnimatorTooltips.Attach(autoKey, new ToggleMode(AnimatorMode.AutoKey));
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
        full.AddComponent(new Switches(AnimatorModeTags.Of(AnimatorMode.FullKeys)));
        AnimatorTooltips.Attach(full, new ToggleMode(AnimatorMode.FullKeys));
        ui.End();
        refs.Ui.ClipName = ClipNameField(ui);

        // Loop stays above the timeline: it changes what the clip does. GAME
        // and ALL live on the preview bar only.
        refs.Ui.LoopButton = HeaderButton(ui, "LOOP", new ToggleLoop(), 76);
        ui.End();
        ui.End();
        ui.End();
    }

    private static Entity ClipNameField(UiBuilder ui) =>
        TextInput(ui, new Vector2(150, AnimatorTimelineModel.HeaderHeight));
}
