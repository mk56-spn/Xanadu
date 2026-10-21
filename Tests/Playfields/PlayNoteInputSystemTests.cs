using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Input;
using Writer.Systems.Playfields;

namespace Writer.Tests.Playfields;

[TestFixture]
public sealed class PlayNoteInputSystemTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private ScriptedPointer _pointer = null!;
    private Entity _scoreText;
    private Entity _comboText;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _root = new SystemRoot(_store);
        _pointer = new ScriptedPointer();
        _scoreText = _store.CreateEntity(new Text(""));
        _comboText = _store.CreateEntity(new Text(""));
        _root.Add(new PlayNoteInputSystem(_store, _pointer));
        _root.Add(new PlayScoreSystem());
    }

    [Test]
    public void PerfectHitAwardsFullNoteValue()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        Entity note = CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_000);
        Press(new Vector2(500, 500));

        Frame();

        Assert.That(note.IsNull || !note.HasComponent<Note>(), Is.True);
        ScoreState score = playfield.GetComponent<ScoreState>();
        Assert.That(score.Score, Is.EqualTo(1_000_000));
        Assert.That(score.NotesHit, Is.EqualTo(1));
        Assert.That(score.Combo, Is.EqualTo(1));
        Assert.That(_scoreText.GetComponent<Text>().Value, Is.EqualTo("SCORE 1000000"));
        Assert.That(_comboText.GetComponent<Text>().Value, Is.EqualTo("COMBO 1"));
    }

    [Test]
    public void ScoreFallsOffLinearlyAcrossTheWindow()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_004);
        Press(new Vector2(500, 500));

        Frame();

        Assert.That(playfield.GetComponent<ScoreState>().Score, Is.EqualTo(500_000));
    }

    [Test]
    public void MissedNoteResetsComboAndScoresNothing()
    {
        Entity playfield = CreatePlayfield(totalNotes: 2);
        CreateNote(1_000, new Vector2(500, 500));
        Entity missed = CreateNote(2_000, new Vector2(200, 200));
        SetPlayhead(playfield, 1_000);
        Press(new Vector2(500, 500));
        Frame();

        Assert.That(playfield.GetComponent<ScoreState>().Combo, Is.EqualTo(1));

        Release();
        SetPlayhead(playfield, 2_009);
        Frame();

        Assert.That(missed.Tags.Has<NoteMissed>(), Is.True);
        Assert.That(playfield.GetComponent<ScoreState>().Combo, Is.EqualTo(0));
        Assert.That(playfield.GetComponent<ScoreState>().Score, Is.EqualTo(500_000));
    }

    [Test]
    public void PausedPlayfieldIgnoresPointerHits()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1, paused: true);
        Entity note = CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_000);
        Press(new Vector2(500, 500));

        Frame();

        Assert.That(note.HasComponent<Note>(), Is.True);
        Assert.That(playfield.GetComponent<ScoreState>().Score, Is.Zero);
        Assert.That(playfield.GetComponent<ScoreState>().Combo, Is.Zero);
    }

    [Test]
    public void HitsOutsideTheTimingWindowDoNotScore()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        Entity note = CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_010);
        Press(new Vector2(500, 500));

        Frame();

        Assert.That(note.Tags.Has<NoteMissed>(), Is.True);
        Assert.That(playfield.GetComponent<ScoreState>().Score, Is.Zero);
    }

    private Entity CreatePlayfield(int totalNotes, bool paused = false)
    {
        Entity playfield = _store.CreateEntity(
            new Playhead(0, 120, 0, false),
            new PlayfieldView(Vector2.Zero, 1, new Vector2(1_000, 1_000)),
            new Playfield(1_000, 1_000),
            new PlayfieldMode(PlayfieldKind.Play),
            new ScoreState(_scoreText, _comboText, totalNotes));

        if (paused)
            playfield.AddTag<PlayPaused>();

        return playfield;
    }

    private Entity CreateNote(long timeMs, Vector2 position) =>
        _store.CreateEntity(new Note(timeMs, position));

    private void SetPlayhead(Entity playfield, long timeMs) =>
        playfield.GetComponent<Playhead>().TimeMs = timeMs;

    private void Press(Vector2 position)
    {
        _pointer.Position = position;
        _pointer.Pressed = true;
    }

    private void Release() => _pointer.Pressed = false;

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private sealed class ScriptedPointer : IPointerInputSource
    {
        public Vector2 Position;
        public bool Pressed;

        public PointerInput Read() => new(Position, Pressed);
    }
}
