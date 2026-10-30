using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Scoring;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Regression: a press only hits a note whose key it matches. Unbound keys and
/// the wrong bound key must leave every note alone.
/// </summary>
public sealed class NoteKeyCheckingSceneTest : SceneTest
{
    public override string Name => "Note key checking";
    public override string Slug => "note-key-checking";

    protected override void SetUpSteps()
    {
        AddStep("start two-key chart", () =>
        {
            var song = TestContent.Song("visual-key-check", "Key Check");
            LevelDocument document = TestContent.Level("Key Check Test", song);
            document.Notes.Add(new NoteDocument
            {
                TimeMs = 1_000,
                X = 500,
                Y = 500,
                Target = KeybindAction.L1,
                Kind = NoteKind.Hit,
            });
            document.Notes.Add(new NoteDocument
            {
                TimeMs = 1_000,
                X = 500,
                Y = 500,
                Target = KeybindAction.R1,
                Kind = NoteKind.Hit,
            });
            Context.Scenes.StartPlay(
                new LevelEntry("memory://key-check", document, song));
        });
        AddUntilStep(
            "wait for the session",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0]
                      .GetComponent<PlaySession>().Started);
        AddStep("freeze the two notes under the player", FreezeNotes);
        AddStep("press an unbound key", () => Press(Key.Z));
        AddWaitStep("unbound key settles", 2);
        AddAssert(
            "unbound key hit nothing",
            () => NoteCount() == 2 && NotesHit() == 0);
        AddStep("press the wrong bound key", () => Press(Key.Space));
        AddWaitStep("wrong key settles", 2);
        AddAssert(
            "wrong key hit nothing",
            () => NoteCount() == 2 && NotesHit() == 0);
        AddStep("press the left key", () => Press(Key.S));
        AddWaitStep("left key settles", 2);
        AddAssert(
            "left key hit only the left note",
            () => NoteCount() == 1 &&
                  NotesHit() == 1 &&
                  RemainingTarget() == KeybindAction.R1);
        AddStep("press the right key", () => Press(Key.J));
        AddWaitStep("right key settles", 2);
        AddAssert(
            "right key hit the right note",
            () => NoteCount() == 0 && NotesHit() == 2);
    }

    private void FreezeNotes()
    {
        Entity playfield = Context.Entities.Query<PlayfieldView>().ToEntityList()[0];
        PlayfieldView view = playfield.GetComponent<PlayfieldView>();
        Entity player = Context.Entities.Query<Player, Position>().ToEntityList()[0];
        Vector2 playerPosition = view.ScreenToPlayfield(
            player.GetComponent<Position>().Value);
        foreach (Entity entity in Context.Entities.Query<Note>().ToEntityList())
        {
            ref Note note = ref entity.GetComponent<Note>();
            note.Position = playerPosition;
        }

        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        playhead.TimeMs = 1_000;
        playhead.Playing = false;
    }

    private void Press(Key key)
    {
        Context.Keyboard.Press(key);
    }

    private int NoteCount() => Context.Entities.Query<Note>().Count;

    private int NotesHit() =>
        Context.Entities.Query<ScoreState>().Count == 0
            ? 0
            : Context.Entities.Query<ScoreState>().ToEntityList()[0]
                .GetComponent<ScoreState>().NotesHit;

    private KeybindAction RemainingTarget() =>
        Context.Entities.Query<Note>().ToEntityList()[0]
            .GetComponent<Note>().Target;
}
