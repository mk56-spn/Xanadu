using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Scoring;
using Writer.Settings;
using Writer.Systems.Input;
using Writer.Systems.Playfields;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.Playfields;

[TestFixture]
public sealed class PlayNoteInputSystemTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private FakeKeyboard _keyboard = null!;
    private KeybindSettings _keybinds = null!;
    private Entity _scoreText;
    private Entity _comboText;
    private Entity _accuracyText;
    private Entity _judgementText;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _root = new SystemRoot(_store);
        _keyboard = new FakeKeyboard();
        _keybinds = new KeybindSettings(persist: false);
        _scoreText = _store.CreateEntity(new Text(""));
        _comboText = _store.CreateEntity(new Text(""));
        _accuracyText = _store.CreateEntity(new Text(""));
        _judgementText = _store.CreateEntity(
            new Text(""),
            new Position(Vector2.Zero));
        _root.Add(new PlayNoteInputSystem(_store, _keybinds, _keyboard));
        _root.Add(new PlayScoreSystem());
    }

    [Test]
    public void CorrectKeyWhileOnTheNoteAwardsAJudgement()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.M);

        Frame();

        ScoreState score = playfield.GetComponent<ScoreState>();
        Assert.That(score.Score, Is.EqualTo(1_000_000));
        Assert.That(score.Accuracy, Is.EqualTo(1.0).Within(0.0001));
        Assert.That(score.Combo, Is.EqualTo(1));
        Assert.That(score.LastJudgement, Is.EqualTo(Judgement.FlawlessP));
        Assert.That(_judgementText.GetComponent<Text>().Value, Is.EqualTo("FLAWLESS+"));
        Assert.That(
            _judgementText.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(500, 500)));
    }

    [TestCase(KeybindAction.L1)]
    [TestCase(KeybindAction.L2)]
    [TestCase(KeybindAction.L3)]
    [TestCase(KeybindAction.M)]
    [TestCase(KeybindAction.R1)]
    [TestCase(KeybindAction.R2)]
    [TestCase(KeybindAction.R3)]
    public void EveryNoteTypeHitsWithItsCorrectKey(KeybindAction target)
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(500, 500), target);
        SetPlayhead(playfield, 1_000);
        Press(target);

        Frame();

        Assert.That(_store.Query<Note>().Count, Is.Zero);
        Assert.That(
            playfield.GetComponent<ScoreState>().LastJudgement,
            Is.EqualTo(Judgement.FlawlessP));
    }

    [TestCase(KeybindAction.L1)]
    [TestCase(KeybindAction.L2)]
    [TestCase(KeybindAction.L3)]
    [TestCase(KeybindAction.M)]
    [TestCase(KeybindAction.R1)]
    [TestCase(KeybindAction.R2)]
    [TestCase(KeybindAction.R3)]
    public void SameKeyDoesNotHitWhenCharacterIsNotOverTheNote(
        KeybindAction target)
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        Entity note = CreateNote(1_000, new Vector2(100, 100), target);
        SetPlayhead(playfield, 1_000);
        SetPlayerPosition(playfield, new Vector2(500, 500));
        Press(target);

        Frame();

        Assert.That(note.HasComponent<Note>(), Is.True);
        Assert.That(playfield.GetComponent<ScoreState>().Score, Is.Zero);
    }

    [Test]
    public void WrongKeyDoesNotHitTheNote()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        Entity note = CreateNote(1_000, new Vector2(500, 500), KeybindAction.R3);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.M);

        Frame();

        Assert.That(note.HasComponent<Note>(), Is.True);
        Assert.That(playfield.GetComponent<ScoreState>().Score, Is.Zero);
    }

    [Test]
    public void TargetKeyHitsEvenWhenItIsNotTheDefault()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(500, 500), KeybindAction.R3);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.R3);

        Frame();

        Assert.That(
            playfield.GetComponent<ScoreState>().LastJudgement,
            Is.EqualTo(Judgement.FlawlessP));
    }

    [Test]
    public void ReboundTargetKeyIsUsed()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(500, 500), KeybindAction.L1);
        SetPlayhead(playfield, 1_000);

        _keybinds.BeginCapture(KeybindAction.L1);
        _keybinds.TryCapture(Key.A);
        _keyboard.Press(Key.A);

        Frame();

        Assert.That(
            playfield.GetComponent<ScoreState>().LastJudgement,
            Is.EqualTo(Judgement.FlawlessP));
    }

    [Test]
    public void OverlappingNotesOnlyConsumeTheMatchingTarget()
    {
        Entity playfield = CreatePlayfield(totalNotes: 2);
        Entity left = CreateNote(1_000, new Vector2(500, 500), KeybindAction.L1);
        Entity right = CreateNote(1_000, new Vector2(500, 500), KeybindAction.R3);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.L1);

        Frame();

        Assert.That(left.IsNull || !left.HasComponent<Note>(), Is.True);
        Assert.That(right.HasComponent<Note>(), Is.True);
        Assert.That(playfield.GetComponent<ScoreState>().NotesHit, Is.EqualTo(1));
    }

    [Test]
    public void PlayerMustBeOverTheNote()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(100, 100));
        SetPlayerPosition(playfield, new Vector2(500, 500));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.M);

        Frame();

        Assert.That(playfield.GetComponent<ScoreState>().Score, Is.Zero);
    }

    [Test]
    public void ScoreIsSeparateFromAccuracy()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_010);
        Press(KeybindAction.M);

        Frame();

        ScoreState score = playfield.GetComponent<ScoreState>();
        Assert.That(score.LastJudgement, Is.EqualTo(Judgement.Flawless));
        Assert.That(score.Score, Is.EqualTo(1_000_000));
        Assert.That(score.Accuracy, Is.EqualTo(0.99).Within(0.0001));
        Assert.That(ScoreRules.RankFor(score.Accuracy), Is.EqualTo("SS"));
    }

    [Test]
    public void MissJudgementHitClearsCombo()
    {
        Entity playfield = CreatePlayfield(totalNotes: 2);
        CreateNote(1_000, new Vector2(500, 500));
        CreateNote(2_000, new Vector2(200, 200));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.M);
        Frame();
        Release(KeybindAction.M);
        Frame();

        SetPlayhead(playfield, 2_100);
        SetPlayerPosition(playfield, new Vector2(200, 200));
        Press(KeybindAction.M);
        Frame();

        ScoreState score = playfield.GetComponent<ScoreState>();
        Assert.That(score.LastJudgement, Is.EqualTo(Judgement.Miss));
        Assert.That(score.NotesHit, Is.EqualTo(1));
        Assert.That(score.Combo, Is.Zero);
        Assert.That(score.Score, Is.EqualTo(500_000));
        Assert.That(_judgementText.GetComponent<Text>().Value, Is.EqualTo("MISS"));
    }

    [Test]
    public void MissedNoteResetsComboAndScoresNothing()
    {
        Entity playfield = CreatePlayfield(totalNotes: 2);
        CreateNote(1_000, new Vector2(500, 500));
        Entity missed = CreateNote(2_000, new Vector2(200, 200));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.M);
        Frame();
        Release(KeybindAction.M);
        SetPlayhead(playfield, 2_151);
        Frame();

        ScoreState score = playfield.GetComponent<ScoreState>();
        Assert.That(missed.Tags.Has<NoteMissed>(), Is.True);
        Assert.That(score.LastJudgement, Is.EqualTo(Judgement.Miss));
        Assert.That(score.NotesHit, Is.EqualTo(1));
        Assert.That(score.Combo, Is.Zero);
        Assert.That(score.Score, Is.EqualTo(500_000));
    }

    [Test]
    public void MissOnlyTriggersAfterTheWindowExpires()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        Entity note = CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_150);
        Frame();
        Assert.That(note.Tags.Has<NoteMissed>(), Is.False);

        SetPlayhead(playfield, 1_151);
        Frame();

        Assert.That(note.Tags.Has<NoteMissed>(), Is.True);
        Assert.That(
            playfield.GetComponent<ScoreState>().LastJudgement,
            Is.EqualTo(Judgement.Miss));
        Assert.That(
            playfield.GetComponent<ScoreState>().Score,
            Is.Zero);
    }

    [Test]
    public void PausedPlayfieldIgnoresHits()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1, paused: true);
        Entity note = CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.M);

        Frame();

        Assert.That(note.HasComponent<Note>(), Is.True);
        Assert.That(playfield.GetComponent<ScoreState>().Score, Is.Zero);
    }

    [Test]
    public void HitsOutsideTheJudgementWindowAutoMiss()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        Entity note = CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_151);
        Press(KeybindAction.M);

        Frame();

        Assert.That(note.Tags.Has<NoteMissed>(), Is.True);
        Assert.That(playfield.GetComponent<ScoreState>().Score, Is.Zero);
        Assert.That(
            playfield.GetComponent<ScoreState>().LastJudgement,
            Is.EqualTo(Judgement.Miss));
    }

    private Entity CreatePlayfield(int totalNotes, bool paused = false)
    {
        Entity playfield = _store.CreateEntity(
            new Playhead(0, 120, 0, false),
            new PlayfieldView(Vector2.Zero, 1, new Vector2(1_000, 1_000)),
            new Playfield(1_000, 1_000),
            new PlayfieldMode(PlayfieldKind.Play),
            new Player(Player.DefaultSize),
            new Position(new Vector2(500, 500)),
            new ScoreState(
                _scoreText,
                _comboText,
                _accuracyText,
                _judgementText,
                totalNotes));

        if (paused)
            playfield.AddTag<PlayPaused>();

        return playfield;
    }

    private Entity CreateNote(
        long timeMs,
        Vector2 position,
        KeybindAction target = KeybindAction.M) =>
        _store.CreateEntity(new Note(timeMs, position, target: target));

    private void SetPlayhead(Entity playfield, long timeMs) =>
        playfield.GetComponent<Playhead>().TimeMs = timeMs;

    private static void SetPlayerPosition(Entity playfield, Vector2 position) =>
        playfield.GetComponent<Position>().Value = position;

    private void Press(KeybindAction action)
    {
        _keyboard.Press(_keybinds.Get(action));
    }

    private void Release(KeybindAction action)
    {
        _keyboard.Release(_keybinds.Get(action));
    }

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private sealed class FakeKeyboard : IKeyboardState
    {
        private readonly HashSet<Key> _down = new();

        public bool IsPressed(Key key) => _down.Contains(key);

        public void Press(Key key) => _down.Add(key);

        public void Release(Key key) => _down.Remove(key);
    }
}
