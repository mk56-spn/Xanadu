using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Tree;
using static Xanadu.Theme.UiMetrics;
using static Xanadu.Ui.Tree.UiSpecs;

namespace Xanadu.Scenes;

/// <summary>
/// A separate screen for editing shared song timing: the song's tempo and offset,
/// stepped or typed exactly, and its BPM changes as a pooled list. The controller
/// fills the live labels, the fields and the list's rows from the song each frame
/// (<see cref="SongTimingView"/>).
/// </summary>
public static class SongTimingScene
{
    /// <summary>The song name, and the empty-list note under the header.</summary>
    private const float NoteWidth = 900;

    /// <summary>The BPM and offset readouts between their step buttons, wider than their text so the buttons stay put.</summary>
    private const float ValueWidth = 500;

    private const float StepWidth = 150;
    private const float ActionWidth = 200;

    /// <summary>The BPM changes list: the header and the rows are this wide.</summary>
    private const float ListWidth = 1000;

    private const float RowHeight = Control.Md;

    /// <summary>Where in the bar a change falls, as text beside its time.</summary>
    private const float PositionWidth = 250;

    public static void Build(
        EntityStore entities,
        Entity root,
        SongDocument song,
        IVisualRidFactory? visuals = null)
    {
        UiNode ui = UiNode.Layer(entities, root, UiLayers.Menu, visuals, UiStage.Full);
        ui.Rect(SceneUi.BackgroundColor).Cover().Below();
        UiNode page = ui.Page().Cross(StackCrossAlignment.Center).Pad(Space.Xxl).Gap(Space.Lg);

        Label title, bpm, offset, empty, foot;
        PooledList list;
        var rows = new List<ChangeRowParts>();
        page.Add(
            new Label("SONG TIMING", UiType.Title, ink: Ink.Accent),
            title = new Label(song.Title, UiType.Section, align: Align.Center, width: NoteWidth, fit: true),
            Stepper(bpm = Readout($"BPM {song.Bpm}"), "-1 BPM", "+1 BPM", SongTimingAdjustKind.Bpm, 1),
            Stepper(offset = Readout($"OFFSET {song.OffsetMs} MS"), "-10 MS", "+10 MS", SongTimingAdjustKind.Offset, 10),

            // Exact values for the first section: a fractional BPM, its bar length, the offset.
            new Row(gap: Space.Md)
            {
                Caption("BPM"),
                Field(SongTimingFieldKind.BaseBpm, 0, "The tempo from the offset on, until the first change (128.5 works)"),
                Caption("BEATS / BAR"),
                Field(SongTimingFieldKind.BaseBar, 0, "Beats in a bar for the first section"),
                Caption("OFFSET MS"),
                Field(SongTimingFieldKind.Offset, 0, "Where beat 1 falls in the audio"),
            },

            new Column(width: ListWidth, grow: 1, cross: Cross.Stretch, gap: Space.Sm)
            {
                new Row(gap: Space.Md)
                {
                    new Label("BPM CHANGES", UiType.Section, ink: Ink.Accent),
                    new Spacer(),
                    new Button("+ CHANGE", UiType.Body, tone: Tone.Primary, cmd: Cmd(SongTempoAction.Add),
                        tip: "Add a change four bars after the last section, at its tempo"),
                },
                (empty = new Label("One tempo for the whole song. + CHANGE adds a section with its own BPM.",
                    UiType.Body, ink: Ink.Dim, align: Align.Center)),
                (list = new PooledList(RowHeight, Space.Sm, grow: 1)
                {
                    Row = slot => ChangeRow(slot, rows),
                }),
                (foot = new Label("", UiType.Small, ink: Ink.Dim, align: Align.Center)),
            },

            new Row(gap: Space.Lg)
            {
                Action("SAVE", Tone.Primary, Cmd(SongTempoAction.Save)),
                Action("ADD SONG…", Tone.Secondary, Cmd(SceneActionId.AddSong)),
                Action("BACK", Tone.Secondary, Cmd(SceneActionId.Back)),
            });

        // The rows' parts have nodes now that the tree is built.
        root.AddComponent(new SongTimingView(title, bpm, offset)
        {
            List = list,
            Rows = rows.ConvertAll(row => row.Ui).ToArray(),
            Empty = empty,
            Foot = foot,
        });
    }

    /// <summary>A pooled row's specs, kept until the build gives them nodes.</summary>
    private sealed record ChangeRowParts(Field At, Label Position, Field Bpm, Field Bar, Button Remove)
    {
        public SongTempoRowUi Ui => new(At, Position, Bpm, Bar, Remove);
    }

    /// <summary>One pooled BPM change row; its fields and remove button act on the change the row shows.</summary>
    private static IEnumerable<UiSpec> ChangeRow(int slot, List<ChangeRowParts> rows)
    {
        Field at = Field(SongTimingFieldKind.ChangeAt, slot, "When the change starts; it moves onto the next whole beat");
        Label position = new("", UiType.Body, ink: Ink.Dim, width: PositionWidth, fit: true);
        Field bpm = Field(SongTimingFieldKind.ChangeBpm, slot, "The tempo from this change on");
        Field bar = Field(SongTimingFieldKind.ChangeBar, slot, "Beats in a bar from this change on");
        Button remove = new("X", UiType.Body, tone: Tone.Secondary, cmd: Cmd(SongTempoAction.Remove, slot),
            width: RowHeight, tip: "Remove this change");
        rows.Add(new ChangeRowParts(at, position, bpm, bar, remove));
        return [Caption("AT MS"), at, position, Caption("BPM"), bpm, Caption("BAR"), bar, new Spacer(), remove];
    }

    /// <summary>A live value between its step buttons: down by <paramref name="step"/> on the left, up on the right.</summary>
    private static Row Stepper(Label value, string down, string up, SongTimingAdjustKind kind, int step) => new(gap: Space.Xl)
    {
        new Button(down, UiType.Body, tone: Tone.Secondary, cmd: Cmd(kind, -step), width: StepWidth),
        value,
        new Button(up, UiType.Body, tone: Tone.Secondary, cmd: Cmd(kind, step), width: StepWidth),
    };

    /// <summary>A value the controller rewrites, in a fixed box so the step buttons beside it never move.</summary>
    private static Label Readout(string text) => new(text, UiType.Heading, align: Align.Center, width: ValueWidth);

    private static Label Caption(string text) => new(text, UiType.Body, ink: Ink.Dim);

    private static Field Field(SongTimingFieldKind kind, int slot, string tip) =>
        new(type: UiType.Body, numeric: true, width: FieldWidth(kind), height: RowHeight, tip: tip)
        {
            Style = node => node.With(new SongTimingField(kind, slot)),
        };

    /// <summary>Each field is as wide as the values it takes: times and tempos wider than bar lengths.</summary>
    private static float FieldWidth(SongTimingFieldKind kind) => kind switch
    {
        SongTimingFieldKind.BaseBar or SongTimingFieldKind.ChangeBar => Control.Xl + Space.Md,
        _ => Control.Xl * 2,
    };

    private static Button Action(string label, Tone tone, UiCommand cmd) =>
        new(label, UiType.Label, tone: tone, cmd: cmd, width: ActionWidth, height: Control.Lg);
}
