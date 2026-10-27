using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Levels;
using Writer.Scenes;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.VisualTests;

/// <summary>
/// Groups in a play scene: items move by the sum of their groups' moves, hold
/// where a move ends, take the latest colour, and hit areas follow.
/// </summary>
public sealed class PlayGroupsSceneTest : SceneTest
{
    public override string Name => "Play groups";
    public override string Slug => "play-groups";

    protected override void SetUpSteps()
    {
        AddStep("open a play scene with groups", OpenPlay);
        AddUntilStep(
            "wait for the play scene",
            () => VisualAssert.IsSceneReady(Context, SceneKind.Play, SceneUi.PlayHudLayer) &&
                  Context.Entities.Query<Note>().Count == 3);
        AddAssert("the grouped start point offsets the spawn", SpawnIsOffset);

        AddStep("go to 0.5 s", () => Seek(500));
        AddWaitStep("settle", 3);
        AddAssert("half way through the move", () => Near(NoteAt(0).X, 200 + 150f) && Near(NoteAt(0).Y, 500));

        AddStep("go to 3 s", () => Seek(3000));
        AddWaitStep("settle", 3);
        AddAssert("the move holds after it ends", () => Near(NoteAt(0).X, 200 + 300f));
        AddAssert("two groups add their moves", () => Near(NoteAt(1).X, 700 + 300f) && Near(NoteAt(1).Y, 500 - 120f));
        AddAssert("the colour is applied", () => GroupColor.For(NoteEntity(2), Colors.Black).G > 0.9f);

        AddStep("go back to 0 s", () => Seek(0));
        AddWaitStep("settle", 3);
        AddAssert("going back undoes it", () => Near(NoteAt(0).X, 200) && Near(NoteAt(1).Y, 500));
        AddStep("go to 2 s, when the notes are due", () => Seek(2000));
        AddWaitStep("hold for a look", 6);
    }

    private void OpenPlay()
    {
        var song = TestContent.Song("visual-groups", "Visual Groups");
        LevelDocument document = TestContent.Level("Group Play Test", song);

        document.Notes.Add(new NoteDocument { TimeMs = 2_000, X = 200, Y = 500, Groups = { 0 } });
        document.Notes.Add(new NoteDocument { TimeMs = 2_000, X = 700, Y = 500, Groups = { 0, 1 } });
        document.Notes.Add(new NoteDocument { TimeMs = 2_000, X = 450, Y = 300, Groups = { 2 } });
        document.Blocks.Add(new BlockDocument { X = 100, Y = 800, Width = 200, Height = 40, Groups = { 0 } });
        document.StartPoints.Add(new StartPointDocument
        {
            Name = "Intro",
            TimeMs = 500,
            X = 100,
            Y = 100,
            Groups = { 0 },
        });

        document.Groups.Add(new GroupDocument
        {
            Id = 0,
            Effects = { new GroupEffectDocument { Kind = GroupEffectKind.Move, Start = 0f, End = 1f, X = 300, Y = 0 } },
        });
        document.Groups.Add(new GroupDocument
        {
            Id = 1,
            Effects = { new GroupEffectDocument { Kind = GroupEffectKind.Move, Start = 0.5f, End = 1.5f, X = 0, Y = -120 } },
        });
        document.Groups.Add(new GroupDocument
        {
            Id = 2,
            Effects = { new GroupEffectDocument { Kind = GroupEffectKind.Color, Start = 1f, End = 2f, Tint = "#00ff00ff" } },
        });

        Context.Scenes.StartPlay(
            new LevelEntry("visual://groups", document, song),
            new StartPoint(
                "Intro",
                500,
                new Vector2(100, 100),
                System.Numerics.Vector2.Zero,
                new Vector2(0, -1),
                ItemGroups.Bit(0)));
    }

    /// <summary>At 0.5 s group 0 has moved 150 px right; the player starts there.</summary>
    private bool SpawnIsOffset()
    {
        Entity playfield = Context.Entities.Query<Player, PlayfieldView>().ToEntityList()[0];
        PlayfieldView view = playfield.GetComponent<PlayfieldView>();
        Vector2 expected = view.PlayfieldToScreen(new Vector2(250, 100));
        Vector2 actual = playfield.GetComponent<Position>().Value;
        return actual.DistanceTo(expected) < 2f;
    }

    private void Seek(long ms)
    {
        foreach (Entity entity in Context.Entities.Query<Playhead, PlayfieldMode>().ToEntityList())
        {
            ref Playhead playhead = ref entity.GetComponent<Playhead>();
            playhead.TimeMs = ms;
            playhead.SeekRequestMs = ms;
            playhead.Playing = false;
        }
    }

    private Entity NoteEntity(int index)
    {
        var notes = Context.Entities.Query<Note>().ToEntityList();
        // Notes come back in creation order, which is the order the document lists them.
        return notes[index];
    }

    private Vector2 NoteAt(int index) => NoteEntity(index).GetComponent<Note>().Position;

    private static bool Near(float a, float b) => System.Math.Abs(a - b) < 2f;
}
