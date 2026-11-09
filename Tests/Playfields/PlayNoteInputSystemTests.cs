using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Scoring;
using Xanadu.Settings;
using Xanadu.Systems.Characters;
using Xanadu.Systems.Input;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Playfields;
using Position = Xanadu.Ecs.Position;
using NVector2 = System.Numerics.Vector2;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class PlayNoteInputSystemTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private Box2DPhysicsWorld _physics = null!;
    private FakeKeyboard _keyboard = null!;
    private KeybindSettings _keybinds = null!;
    private OffsetSettings _offsets = null!;
    private Entity _scoreText;
    private Entity _comboText;
    private Entity _accuracyText;
    private Entity _judgementText;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _root = new SystemRoot(_store);
        _physics = new Box2DPhysicsWorld();
        _keyboard = new FakeKeyboard();
        _keybinds = new KeybindSettings(persist: false);
        _offsets = new OffsetSettings(persist: false);
        _scoreText = _store.CreateEntity(new Text(""));
        _comboText = _store.CreateEntity(new Text(""));
        _accuracyText = _store.CreateEntity(new Text(""));
        _judgementText = _store.CreateEntity(
            new Text(""),
            new TextStyle(28, Colors.White),
            new Position(Vector2.Zero));
        _root.Add(new PlayNoteInputSystem(_store, _keybinds, _keyboard, _offsets));
        _root.Add(new ContactBarrierSystem(_store, _offsets));
        _root.Add(new PlayScoreSystem());
    }

    [TearDown]
    public void TearDown() => _physics.Dispose();

    [Test]
    public void CorrectKeyWhileOnTheNoteAwardsAJudgement()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action2);

        Frame();

        ScoreState score = playfield.GetComponent<ScoreState>();
        Assert.That(score.Score, Is.EqualTo(1_000_000));
        Assert.That(score.Accuracy, Is.EqualTo(1.0).Within(0.0001));
        Assert.That(score.Combo, Is.EqualTo(1));
        Assert.That(score.LastJudgement, Is.EqualTo(Judgement.FlawlessP));
        Assert.That(score.FlawlessPCount, Is.EqualTo(1));
        var trails = _store.Query<HitTrail>().ToEntityList();
        Assert.That(trails, Has.Count.EqualTo(1));
        Assert.That(trails[0].GetComponent<HitTrail>().Label, Is.EqualTo("SPACE"));
        Assert.That(
            trails[0].GetComponent<HitTrail>().Position,
            Is.EqualTo(new Vector2(500, 500)));
        Assert.That(_judgementText.GetComponent<Text>().Value, Is.EqualTo("FLAWLESS+"));
        Assert.That(
            _judgementText.GetComponent<TextStyle>().Color,
            Is.EqualTo(PlayScoreSystem.ColorFor(Judgement.FlawlessP)));
        Assert.That(
            _judgementText.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(500, 500)));
    }

    [Test]
    public void HittingANoteAboveThePlayerTriggersTheHighKick()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new CharacterRig(AttackTestRig()));
        CreateNote(1_000, new Vector2(500, 450)); // player sits at screen y 500; this note is above it, in reach.
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action2);

        Frame();

        Assert.That(playfield.GetComponent<CharacterAttack>().Clip, Is.EqualTo(ContactClip.AttackHigh));
        Assert.That(
            playfield.GetComponent<CharacterRig>().Runtime.Additives[0].Clip.Id,
            Is.EqualTo(ContactClip.AttackHigh.Id()));
    }

    [Test]
    public void HittingANoteToTheLeftFlipsTheCharacterLeft()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new CharacterRig(AttackTestRig()));
        CreateNote(1_000, new Vector2(420, 500));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action2);

        Frame();

        Assert.That(playfield.GetComponent<CharacterFacing>().Left, Is.True);
        Assert.That(
            playfield.GetComponent<CharacterAttack>().Clip,
            Is.EqualTo(ContactClip.AttackMid));
    }

    [Test]
    public void HittingANoteToTheRightFlipsTheCharacterRight()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new CharacterFacing { Left = true });
        CreateNote(1_000, new Vector2(580, 500));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action2);

        Frame();

        Assert.That(playfield.GetComponent<CharacterFacing>().Left, Is.False);
    }

    [Test]
    public void HittingANoteStraightAheadKeepsTheFacing()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new CharacterFacing { Left = true });
        CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action2);

        Frame();

        Assert.That(playfield.GetComponent<CharacterFacing>().Left, Is.True);
    }

    private static AnimatorRuntime AttackTestRig()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone { Id = "root", Parent = -1, Rest = Transform2D.Identity });
        package.Clips.Add(new XafClip { Id = ContactClip.Idle.Id(), Duration = 1f, Loop = true, LoopEnd = 1f });
        package.Clips.Add(new XafClip { Id = ContactClip.AttackLow.Id(), Duration = 0.2f });
        package.Clips.Add(new XafClip { Id = ContactClip.AttackMid.Id(), Duration = 0.2f });
        package.Clips.Add(new XafClip { Id = ContactClip.AttackHigh.Id(), Duration = 0.2f });
        package.Clips.Add(new XafClip { Id = ContactClip.AttackAirLow.Id(), Duration = 0.2f });
        package.Clips.Add(new XafClip { Id = ContactClip.AttackAirMid.Id(), Duration = 0.2f });
        package.Clips.Add(new XafClip { Id = ContactClip.AttackAirHigh.Id(), Duration = 0.2f });
        var runtime = new AnimatorRuntime(package);
        runtime.Play(ContactClip.Idle.Id(), 0f);
        return runtime;
    }

    [Test]
    public void JudgementSitsOnTheNoteItJudged()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        ref PlayfieldView view = ref playfield.GetComponent<PlayfieldView>();
        view = new PlayfieldView(
            new Vector2(400, 100),
            0.5f,
            new Vector2(1000, 1000));

        Entity note = CreateNote(1_000, new Vector2(600, 800));
        Vector2 noteScreen = view.PlayfieldToScreen(
            note.GetComponent<Note>().Position);
        SetPlayerPosition(playfield, noteScreen);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action2);

        Frame();

        Assert.That(_store.Query<Note>().Count, Is.Zero);
        Assert.That(
            _judgementText.GetComponent<Position>().Value,
            Is.EqualTo(noteScreen));
    }

    [Test]
    public void HittingANoteLeavesAPopWhereItWas()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action2);

        Frame();

        var pops = _store.Query<NotePop>().ToEntityList();
        Assert.That(pops, Has.Count.EqualTo(1));
        NotePop pop = pops[0].GetComponent<NotePop>();
        Assert.That(pop.Position, Is.EqualTo(new Vector2(500, 500)));
        Assert.That(pop.Radius, Is.EqualTo(PlayfieldRenderer.NoteSize / 2f));
        Assert.That(pop.Age, Is.GreaterThanOrEqualTo(0f));
        Assert.That(pop.Age, Is.LessThan(pop.Lifetime));
        Assert.That(
            pop.Color,
            Is.EqualTo(PlayfieldRenderer.ColorFor(KeybindAction.Action2)));
        Assert.That(_store.Query<Note>().Count, Is.Zero);
    }

    [Test]
    public void AMissedNoteLeavesNoPop()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_151);
        Press(KeybindAction.Action2);

        Frame();

        Assert.That(_store.Query<NotePop>().Count, Is.Zero);
        Assert.That(
            playfield.GetComponent<ScoreState>().LastJudgement,
            Is.EqualTo(Judgement.Miss));
    }








    [TestCase(KeybindAction.Action1)]
    [TestCase(KeybindAction.Action2)]
    [TestCase(KeybindAction.Action3)]
    public void EachColouredNoteHitsWithItsActionKey(KeybindAction target)
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

    [TestCase(KeybindAction.Action1)]
    [TestCase(KeybindAction.Action2)]
    [TestCase(KeybindAction.Action3)]
    public void SameKeyDoesNotHitWhenCharacterIsNotOverTheNote(KeybindAction target)
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

    [TestCase(KeybindAction.Action1)]
    [TestCase(KeybindAction.Action2)]
    [TestCase(KeybindAction.Action3)]
    public void ANeutralNoteTakesAnyActionKey(KeybindAction action)
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(500, 500), kind: NoteKind.Neutral);
        SetPlayhead(playfield, 1_000);
        Press(action);

        Frame();

        Assert.That(_store.Query<Note>().Count, Is.Zero);
        Assert.That(playfield.GetComponent<ScoreState>().NotesHit, Is.EqualTo(1));
    }

    [TestCase(KeybindAction.Left)]
    [TestCase(KeybindAction.Up)]
    public void DirectionKeysDoNotHitActionNotes(KeybindAction direction)
    {
        Entity playfield = CreatePlayfield(totalNotes: 2);
        CreateNote(1_000, new Vector2(500, 500), kind: NoteKind.Neutral);
        CreateNote(1_000, new Vector2(500, 500), KeybindAction.Action1);
        SetPlayhead(playfield, 1_000);
        Press(direction);

        Frame();

        Assert.That(_store.Query<Note>().Count, Is.EqualTo(2));
    }

    [TestCase(KeybindAction.Action1, 0)]
    [TestCase(KeybindAction.Action2, 1)]
    [TestCase(KeybindAction.Action3, 2)]
    public void ANeutralNoteLoadsTheBarOfTheKeyItIsHitWith(KeybindAction action, int bar)
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfield.AddComponent(new ContactCounters());
        CreateNote(1_000, new Vector2(500, 500), kind: NoteKind.Neutral);
        SetPlayhead(playfield, 1_000);
        Press(action);

        Frame();

        ContactCounters counters = playfield.GetComponent<ContactCounters>();
        for (int i = 0; i < ContactCounters.Bars; i++)
            Assert.That(counters[i], Is.EqualTo(i == bar ? 1 : 0));
    }

    [Test]
    public void AColouredNoteLoadsNoBar()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfield.AddComponent(new ContactCounters());
        CreateNote(1_000, new Vector2(500, 500), KeybindAction.Action1);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action1);

        Frame();

        Assert.That(playfield.GetComponent<ContactCounters>().First, Is.Zero);
    }

    [Test]
    public void BarsCapAtFive()
    {
        var counters = new ContactCounters { Second = ContactCounters.Cap };
        counters.Add(KeybindAction.Action2);
        Assert.That(counters.Second, Is.EqualTo(ContactCounters.Cap));
    }

    [Test]
    public void ContactBarrierResolvesWhenTheReachTouchesItsEdge()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfield.AddComponent(new ContactCounters { First = 1 });
        CreateNote(1_000, new Vector2(635, 500), kind: NoteKind.Contact, costs: new BarCosts(1));
        SetPlayhead(playfield, 1_000);

        Frame();

        Assert.That(_store.Query<Note>().Count, Is.Zero);
    }

    [Test]
    public void APaidBarrierSpendsItsCosts()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfield.AddComponent(new ContactCounters { First = 3, Third = 2 });
        CreateNote(1_000, new Vector2(500, 500), kind: NoteKind.Contact, costs: new BarCosts(2, 0, 1));
        SetPlayhead(playfield, 1_000);

        Frame();

        ContactCounters counters = playfield.GetComponent<ContactCounters>();
        Assert.That(counters.First, Is.EqualTo(1));
        Assert.That(counters.Third, Is.EqualTo(1));
        Assert.That(_store.Query<Note>().Count, Is.Zero);
        Assert.That(playfield.GetComponent<ScoreState>().AccuracyPenalty, Is.Zero);
    }

    [Test]
    public void AShortBarrierSpendsNothingAndCostsAccuracy()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfield.AddComponent(new ContactCounters { First = 3, Second = 1 });
        CreateNote(1_000, new Vector2(500, 500), kind: NoteKind.Contact, costs: new BarCosts(2, 2));
        SetPlayhead(playfield, 1_000);

        Frame();

        ContactCounters counters = playfield.GetComponent<ContactCounters>();
        Assert.That(counters.First, Is.EqualTo(3));
        Assert.That(counters.Second, Is.EqualTo(1));
        ScoreState score = playfield.GetComponent<ScoreState>();
        Assert.That(score.LastJudgement, Is.EqualTo(Judgement.Contact));
        Assert.That(score.AccuracyPenalty, Is.EqualTo(ScoreRules.ContactAccuracyPenalty).Within(0.0001));
    }

    [Test]
    public void ContactBarrierThatExpiresIsNotAScoredMiss()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfield.AddComponent(new ContactCounters { First = 3 });
        Entity barrier = CreateNote(1_000, new Vector2(900, 900), kind: NoteKind.Contact, costs: new BarCosts(1));
        SetPlayhead(playfield, 1_200);

        Frame();

        Assert.That(barrier.IsNull, Is.True);
        ScoreState score = playfield.GetComponent<ScoreState>();
        Assert.That(score.MissCount, Is.Zero);
        Assert.That(score.AccuracyPenalty, Is.Zero);
    }

    [Test]
    public void PausedContactBarrierDoesNotResolve()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1, paused: true);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfield.AddComponent(new ContactCounters { First = 1 });
        Entity barrier = CreateNote(1_000, new Vector2(500, 500), kind: NoteKind.Contact, costs: new BarCosts(1));
        SetPlayhead(playfield, 1_000);

        Frame();

        Assert.That(barrier.HasComponent<Note>(), Is.True);
        Assert.That(playfield.GetComponent<ContactCounters>().First, Is.EqualTo(1));
    }

    [Test]
    public void WrongKeyDoesNotHitTheNote()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        Entity note = CreateNote(1_000, new Vector2(500, 500), KeybindAction.Action3);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action2);

        Frame();

        Assert.That(note.HasComponent<Note>(), Is.True);
        Assert.That(playfield.GetComponent<ScoreState>().Score, Is.Zero);
    }

    [Test]
    public void TargetKeyHitsEvenWhenItIsNotTheDefault()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(500, 500), KeybindAction.Action3);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action3);

        Frame();

        Assert.That(
            playfield.GetComponent<ScoreState>().LastJudgement,
            Is.EqualTo(Judgement.FlawlessP));
    }

    [Test]
    public void ReboundTargetKeyIsUsed()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(500, 500), KeybindAction.Action1);
        SetPlayhead(playfield, 1_000);

        _keybinds.BeginCapture(KeybindAction.Action1);
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
        Entity left = CreateNote(1_000, new Vector2(500, 500), KeybindAction.Action1);
        Entity right = CreateNote(1_000, new Vector2(500, 500), KeybindAction.Action3);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action1);

        Frame();

        Assert.That(left.IsNull || !left.HasComponent<Note>(), Is.True);
        Assert.That(right.HasComponent<Note>(), Is.True);
        Assert.That(playfield.GetComponent<ScoreState>().NotesHit, Is.EqualTo(1));
    }

    [Test]
    public void TappingOverAMineCostsOnePercentAccuracy()
    {
        Entity playfield = CreatePlayfield(totalNotes: 10);
        ref ScoreState scoreRef = ref playfield.GetComponent<ScoreState>();
        scoreRef.AccuracyPoints = 10;
        Entity mine = _store.CreateEntity(new Mine(1_000, new Vector2(500, 500)));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action2);

        Frame();

        ScoreState score = playfield.GetComponent<ScoreState>();
        Assert.That(mine.IsNull || !mine.HasComponent<Mine>(), Is.True);
        Assert.That(score.LastJudgement, Is.EqualTo(Judgement.Mine));
        Assert.That(score.Accuracy, Is.EqualTo(0.99).Within(0.0001));
        Assert.That(score.AccuracyPenalty, Is.EqualTo(0.1).Within(0.0001));
        Assert.That(score.Combo, Is.Zero);
        Assert.That(score.Score, Is.Zero);
        Assert.That(_judgementText.GetComponent<Text>().Value, Is.EqualTo("BOOM"));
        Assert.That(
            _judgementText.GetComponent<TextStyle>().Color,
            Is.EqualTo(PlayScoreSystem.ColorFor(Judgement.Mine)));
    }

    [Test]
    public void MineDoesNotTriggerWhenPlayerIsNotOverIt()
    {
        Entity playfield = CreatePlayfield(totalNotes: 10);
        ref ScoreState scoreRef = ref playfield.GetComponent<ScoreState>();
        scoreRef.AccuracyPoints = 10;
        Entity mine = _store.CreateEntity(new Mine(1_000, new Vector2(100, 100)));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action2);

        Frame();

        Assert.That(mine.HasComponent<Mine>(), Is.True);
        Assert.That(playfield.GetComponent<ScoreState>().AccuracyPenalty, Is.Zero);
    }

    [Test]
    public void MineDoesNotTriggerOutsideItsWindow()
    {
        Entity playfield = CreatePlayfield(totalNotes: 10);
        ref ScoreState scoreRef = ref playfield.GetComponent<ScoreState>();
        scoreRef.AccuracyPoints = 10;
        Entity mine = _store.CreateEntity(new Mine(2_000, new Vector2(500, 500)));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action2);

        Frame();

        Assert.That(mine.HasComponent<Mine>(), Is.True);
        Assert.That(playfield.GetComponent<ScoreState>().AccuracyPenalty, Is.Zero);
    }

    [Test]
    public void GlobalOffsetAdjustsLateInput()
    {
        _offsets.Set(40);
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_040);
        Press(KeybindAction.Action2);

        Frame();

        Assert.That(
            playfield.GetComponent<ScoreState>().LastJudgement,
            Is.EqualTo(Judgement.FlawlessP));
    }

    [Test]
    public void PlayerMustBeOverTheNote()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(100, 100));
        SetPlayerPosition(playfield, new Vector2(500, 500));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action2);

        Frame();

        Assert.That(playfield.GetComponent<ScoreState>().Score, Is.Zero);
    }

    [Test]
    public void NoteWithinReachIsHitEvenOffTheThinBox()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        // 90 px to the right: clear of the 30 wide damage box, inside the sphere.
        CreateNote(1_000, new Vector2(590, 500));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action2);

        Frame();

        Assert.That(_store.Query<Note>().Count, Is.Zero);
        Assert.That(
            playfield.GetComponent<ScoreState>().LastJudgement,
            Is.EqualTo(Judgement.FlawlessP));
    }

    [Test]
    public void NoteBeyondReachIsNotHit()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        Entity note = CreateNote(1_000, new Vector2(620, 500));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action2);

        Frame();

        Assert.That(note.HasComponent<Note>(), Is.True);
        Assert.That(playfield.GetComponent<ScoreState>().Score, Is.Zero);
    }

    [Test]
    public void ThirtySecondBeatHitsAllThreeActionKeysPerfectly()
    {
        const int noteCount = 30;
        Entity playfield = CreatePlayfield(totalNotes: noteCount);
        for (int i = 0; i < noteCount; i++)
        {
            CreateNote(
                1_000 + i * 1_000,
                new Vector2(500, 500),
                KeybindSettings.ActionKeys[i % KeybindSettings.ActionKeys.Length]);
        }

        for (int i = 0; i < noteCount; i++)
        {
            SetPlayhead(playfield, 1_000 + i * 1_000);
            KeybindAction action =
                KeybindSettings.ActionKeys[i % KeybindSettings.ActionKeys.Length];
            Press(action);
            Frame();
            Release(action);
            Frame();
        }

        ScoreState score = playfield.GetComponent<ScoreState>();
        Assert.That(_store.Query<Note>().Count, Is.Zero);
        Assert.That(score.NotesHit, Is.EqualTo(noteCount));
        Assert.That(score.Combo, Is.EqualTo(noteCount));
        Assert.That(score.MaxCombo, Is.EqualTo(noteCount));
        Assert.That(score.Accuracy, Is.EqualTo(1.0).Within(0.0001));
        Assert.That(score.Score, Is.EqualTo(1_000_000));
        Assert.That(score.LastJudgement, Is.EqualTo(Judgement.FlawlessP));
    }

    [Test]
    public void ABeatWithAMineLosesAccuracy()
    {
        Entity playfield = CreatePlayfield(totalNotes: 4);
        for (int i = 0; i < 2; i++)
            CreateNote(1_000 + i * 1_000, new Vector2(500, 500));
        _store.CreateEntity(new Mine(3_000, new Vector2(500, 500)));
        for (int i = 0; i < 2; i++)
            CreateNote(4_000 + i * 1_000, new Vector2(500, 500));

        for (int time = 1_000; time <= 5_000; time += 1_000)
        {
            SetPlayhead(playfield, time);
            Press(KeybindAction.Action2);
            Frame();
            Release(KeybindAction.Action2);
            Frame();
        }

        ScoreState score = playfield.GetComponent<ScoreState>();
        Assert.That(score.AccuracyPenalty, Is.EqualTo(0.04).Within(0.0001));
        Assert.That(score.Accuracy, Is.EqualTo(0.99).Within(0.0001));
        Assert.That(_store.Query<Mine>().Count, Is.Zero);
    }

    [Test]
    public void LateBeatIsNotPerfect()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_035);
        Press(KeybindAction.Action2);
        Frame();

        ScoreState score = playfield.GetComponent<ScoreState>();
        Assert.That(score.LastJudgement, Is.EqualTo(Judgement.Fair));
        Assert.That(score.Accuracy, Is.EqualTo(0.95).Within(0.0001));
    }

    [Test]
    public void ScoreIsSeparateFromAccuracy()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_010);
        Press(KeybindAction.Action2);

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
        Press(KeybindAction.Action2);
        Frame();
        Release(KeybindAction.Action2);
        Frame();

        SetPlayhead(playfield, 2_100);
        SetPlayerPosition(playfield, new Vector2(200, 200));
        Press(KeybindAction.Action2);
        Frame();

        ScoreState score = playfield.GetComponent<ScoreState>();
        Assert.That(score.LastJudgement, Is.EqualTo(Judgement.Miss));
        Assert.That(score.NotesHit, Is.EqualTo(1));
        Assert.That(score.Combo, Is.Zero);
        Assert.That(score.Score, Is.EqualTo(500_000));
        Assert.That(score.MissCount, Is.EqualTo(1));
        Assert.That(_judgementText.GetComponent<Text>().Value, Is.EqualTo("MISS"));
        Assert.That(
            _judgementText.GetComponent<TextStyle>().Color,
            Is.EqualTo(PlayScoreSystem.ColorFor(Judgement.Miss)));
    }

    [Test]
    public void MissedNoteResetsComboAndScoresNothing()
    {
        Entity playfield = CreatePlayfield(totalNotes: 2);
        CreateNote(1_000, new Vector2(500, 500));
        Entity missed = CreateNote(2_000, new Vector2(200, 200));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.Action2);
        Frame();
        Release(KeybindAction.Action2);
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
        Press(KeybindAction.Action2);

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
        Press(KeybindAction.Action2);

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
        KeybindAction target = KeybindAction.Action2,
        NoteKind kind = NoteKind.Hit,
        BarCosts costs = default) =>
        _store.CreateEntity(new Note(timeMs, position, target: target, kind: kind, costs: costs));

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


}
