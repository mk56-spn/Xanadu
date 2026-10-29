using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Theme;

namespace Xanadu.Scenes;

/// <summary>
/// A separate screen for editing shared song timing. The live labels keep fixed
/// boxes because systems rewrite them as the values change; everything else is
/// measured or token-sized.
/// </summary>
public static class SongTimingScene
{
    private const float TitleY = 70;
    private const float SongY = 140;
    private const float BpmY = 250;
    private const float OffsetY = 330;
    private const float ActionsY = 450;

    private const float SongWidth = 900;
    private const float SongHeight = 34;
    private const float ValueWidth = 500;
    private const float ValueHeight = 50;

    private const float AdjustWidth = 150;
    private const float AdjustHeight = 42;
    private const float AdjustOffset = 180;

    private const float ActionWidth = 200;
    private const float ActionGap = 20;

    public static void Build(
        EntityStore entities,
        Entity root,
        SongDocument song,
        IVisualRidFactory? visuals = null)
    {
        UiStage stage = UiStage.Full;
        UiNode ui = UiNode.Layer(entities, root, SceneUi.MenuLayer, visuals, stage);

        ui.Rect(SceneUi.BackgroundColor).Cover().Below();
        ui.Text("SONG TIMING", UiType.Title, SceneUi.AccentColor)
            .At(new Vector2(stage.Center.X, TitleY));

        UiNode title = ui.Text(
                song.Title,
                UiType.Section,
                SceneUi.TextColor,
                TextAlign.Center)
            .Size(SongWidth, SongHeight)
            .At(new Vector2(stage.Center.X, SongY));

        UiNode bpm = ui.Text(
                $"BPM {song.Bpm}",
                UiType.Heading,
                SceneUi.TextColor,
                TextAlign.Center)
            .Size(ValueWidth, ValueHeight)
            .At(new Vector2(stage.Center.X, BpmY));

        UiNode offset = ui.Text(
                $"OFFSET {song.OffsetMs} MS",
                UiType.Heading,
                SceneUi.TextColor,
                TextAlign.Center)
            .Size(ValueWidth, ValueHeight)
            .At(new Vector2(stage.Center.X, OffsetY));

        Adjust(ui, stage, BpmY, -1f, "-1 BPM", SongTimingAdjustKind.Bpm, -1);
        Adjust(ui, stage, BpmY, 1f, "+1 BPM", SongTimingAdjustKind.Bpm, 1);
        Adjust(ui, stage, OffsetY, -1f, "-10 MS", SongTimingAdjustKind.Offset, -10);
        Adjust(ui, stage, OffsetY, 1f, "+10 MS", SongTimingAdjustKind.Offset, 10);

        UiNode actions = ui.Row()
            .Gap(ActionGap)
            .At(new Vector2(stage.Center.X, ActionsY));
        actions.Button("SAVE", UiType.Label, SceneUi.ButtonColor)
            .Size(ActionWidth, UiMetrics.Control.Lg)
            .With(new SongTimingSave());
        actions.Button("ADD SONG…", UiType.Label, SceneUi.MutedButtonColor)
            .Size(ActionWidth, UiMetrics.Control.Lg)
            .On(SceneActionId.AddSong);
        actions.Button("BACK", UiType.Label, SceneUi.MutedButtonColor)
            .Size(ActionWidth, UiMetrics.Control.Lg)
            .On(SceneActionId.Back);

        root.AddComponent(new SongTimingView(title, bpm, offset));
    }

    private static void Adjust(
        UiNode ui,
        UiStage stage,
        float y,
        float side,
        string label,
        SongTimingAdjustKind kind,
        int delta) =>
        ui.Button(label, UiType.Body, SceneUi.MutedButtonColor)
            .Size(AdjustWidth, AdjustHeight)
            .At(new Vector2(stage.Center.X + side * AdjustOffset, y))
            .With(new SongTimingAdjust(kind, delta));
}
