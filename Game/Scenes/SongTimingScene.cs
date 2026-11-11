using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>
/// A separate screen for editing shared song timing. The live labels keep fixed
/// boxes because systems rewrite them as the values change; everything else is
/// measured or token-sized.
/// </summary>
public static class SongTimingScene
{
    private const float TitleY = 70;
    private const float SongY = 130;
    private const float BpmY = 210;
    private const float OffsetY = 280;
    private const float BaseY = 350;
    private const float ChangesY = 430;
    private const float RowsTop = 485;
    private const float RowStep = 46;
    private const float ActionsY = 960;

    /// <summary>BPM change rows shown at once; ▲ ▼ scroll through more.</summary>
    public const int ChangeRows = 9;

    private const float RowHeight = 38;
    private const float RowGap = 12;

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
        UiNode ui = UiNode.Layer(entities, root, UiLayers.Menu, visuals, stage);

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

        // Exact values for the first section: a fractional BPM, its bar length, the offset.
        UiNode first = ui.Row().Gap(RowGap).At(new Vector2(stage.Center.X, BaseY));
        Label(first, "BPM", 60);
        Field(first, SongTimingFieldKind.BaseBpm, 0, 120, "The tempo from the offset on, until the first change (128.5 works)");
        Label(first, "BEATS / BAR", 140);
        Field(first, SongTimingFieldKind.BaseBar, 0, 70, "Beats in a bar for the first section");
        Label(first, "OFFSET MS", 130);
        Field(first, SongTimingFieldKind.Offset, 0, 120, "Where beat 1 falls in the audio");

        UiNode header = ui.Row().Gap(RowGap).At(new Vector2(stage.Center.X, ChangesY));
        header.Text("BPM CHANGES", UiType.Section, SceneUi.AccentColor, TextAlign.Left).Size(420, RowHeight);
        header.Button("+ CHANGE", UiType.Body, SceneUi.ButtonColor)
            .Size(160, RowHeight)
            .Tooltip("Add a change four bars after the last section, at its tempo")
            .With(new SongTempoButton(SongTempoAction.Add));
        header.Button("▲", UiType.Body, SceneUi.MutedButtonColor)
            .Size(RowHeight, RowHeight)
            .With(new SongTempoButton(SongTempoAction.ScrollUp));
        header.Button("▼", UiType.Body, SceneUi.MutedButtonColor)
            .Size(RowHeight, RowHeight)
            .With(new SongTempoButton(SongTempoAction.ScrollDown));

        UiNode empty = ui.Text(
                "One tempo for the whole song. + CHANGE adds a section with its own BPM.",
                UiType.Body,
                SceneUi.DimTextColor,
                TextAlign.Center)
            .Size(SongWidth, RowHeight)
            .At(new Vector2(stage.Center.X, RowsTop));

        var rows = new SongTempoRowUi[ChangeRows];
        for (int slot = 0; slot < ChangeRows; slot++)
        {
            UiNode row = ui.Row().Gap(RowGap).At(new Vector2(stage.Center.X, RowsTop + slot * RowStep));
            Label(row, "AT MS", 70);
            Entity at = Field(row, SongTimingFieldKind.ChangeAt, slot, 130, "When the change starts; it moves onto the next whole beat");
            Entity position = row.Text("", UiType.Body, SceneUi.DimTextColor, TextAlign.Left).Size(250, RowHeight).Entity;
            Label(row, "BPM", 50);
            Entity bpmField = Field(row, SongTimingFieldKind.ChangeBpm, slot, 110, "The tempo from this change on");
            Label(row, "BAR", 50);
            Entity bar = Field(row, SongTimingFieldKind.ChangeBar, slot, 60, "Beats in a bar from this change on");
            Entity remove = row.Button("X", UiType.Body, SceneUi.MutedButtonColor)
                .Size(RowHeight, RowHeight)
                .Tooltip("Remove this change")
                .With(new SongTempoButton(SongTempoAction.Remove, slot))
                .Entity;
            rows[slot] = new SongTempoRowUi(row.Entity, at, position, bpmField, bar, remove);
        }

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

        root.AddComponent(new SongTimingView(title, bpm, offset) { Rows = rows, Empty = empty.Entity });
    }

    private static void Label(UiNode row, string text, float width) =>
        row.Text(text, UiType.Body, SceneUi.DimTextColor, TextAlign.Right).Size(width, RowHeight);

    private static Entity Field(UiNode row, SongTimingFieldKind kind, int slot, float width, string tooltip) =>
        row.Rect(Palette.SurfaceRaised, "", new Vector2(width, RowHeight))
            .Input()
            .With(new TextStyle(16, SceneUi.TextColor))
            .Align(TextAlign.Left)
            .With(new TextField("", numeric: true))
            .With(new SongTimingField(kind, slot))
            .Tooltip(tooltip)
            .Entity;

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
