using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;
using Xanadu.Editor;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Settings;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.VisualTests;

/// <summary>A hit says whether it was early or late, under its judgement.</summary>
public sealed class TimingIndicatorSceneTest : SceneTest
{
    private const long EarlyNoteMs = 1_000;
    private const long LateNoteMs = 3_000;
    private const long HitErrorMs = 30;

    public override string Name => "Early and late indicator";
    public override string Slug => "timing-indicator";

    protected override void SetUpSteps()
    {
        AddStep("start chart", () =>
        {
            var song = TestContent.Song("visual-timing", "Timing");
            LevelDocument document = TestContent.Level("Timing Indicator Test", song);
            document.Notes.Add(DashNote(EarlyNoteMs));
            document.Notes.Add(DashNote(LateNoteMs));
            Context.Scenes.StartPlay(
                new LevelEntry("memory://timing-indicator", document, song));
        });
        AddUntilStep(
            "wait for play",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0]
                      .GetComponent<PlaySession>().Started);
        AddUntilStep("wait for the first note", () => PlayheadTime() >= 900);

        AddHit(EarlyNoteMs - HitErrorMs, notesHit: 1, "EARLY");
        AddHit(LateNoteMs + HitErrorMs, notesHit: 2, "LATE");
    }

    private void AddHit(long timeMs, int notesHit, string label)
    {
        AddStep($"freeze at {timeMs}", () =>
        {
            SetPlayheadTime(timeMs);
            AlignNotesWithPlayer();
        });
        AddStep($"press for {label}", () => Context.Keyboard.Press(
            Context.Systems.Keybinds.Get(KeybindAction.L3)));
        AddUntilStep($"shows {label}", () =>
        {
            Entity playfield = Context.Entities.Query<ScoreState>().ToEntityList()[0];
            Entity text = playfield.GetComponent<TimingIndicator>().Text;
            return playfield.GetComponent<ScoreState>().NotesHit == notesHit &&
                   text.GetComponent<Text>().Value == label &&
                   text.Enabled;
        });
        AddStep($"release after {label}", () => Context.Keyboard.Release(
            Context.Systems.Keybinds.Get(KeybindAction.L3)));
    }

    private static NoteDocument DashNote(long timeMs) => new()
    {
        TimeMs = timeMs,
        X = 500,
        Y = 1000,
        Target = KeybindAction.L3,
        Kind = NoteKind.Dash,
        DashDirection = DashDirection.Right,
    };

    private void AlignNotesWithPlayer()
    {
        Entity player = Context.Entities.Query<Player, Position>().ToEntityList()[0];
        Entity playfield = Context.Entities.Query<PlayfieldView>().ToEntityList()[0];
        Vector2 position = playfield.GetComponent<PlayfieldView>()
            .ScreenToPlayfield(player.GetComponent<Position>().Value);
        foreach (Entity note in Context.Entities.Query<Note>().ToEntityList())
            note.GetComponent<Note>().Position = position;
    }

    private void SetPlayheadTime(long timeMs)
    {
        foreach (Entity entity in Context.Entities.Query<Playhead>().ToEntityList())
        {
            ref Playhead playhead = ref entity.GetComponent<Playhead>();
            playhead.Playing = false;
            playhead.TimeMs = timeMs;
            playhead.SeekRequestMs = -1;
        }
    }

    private long PlayheadTime() =>
        Context.Entities.Query<Playhead>().Count == 0
            ? 0
            : Context.Entities.Query<Playhead>().ToEntityList()[0]
                .GetComponent<Playhead>().TimeMs;
}
