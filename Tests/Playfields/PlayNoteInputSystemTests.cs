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
        _root.Add(new RailHoldSystem(_keyboard, _keybinds));
        _root.Add(new ContactBarrierSystem(_store, _offsets));
        _root.Add(new SplitlineSystem());
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
        Press(KeybindAction.M);

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
        Press(KeybindAction.M);

        Frame();

        Assert.That(playfield.GetComponent<CharacterAttack>().ClipId, Is.EqualTo(CharacterAttackSystem.HighClip));
        Assert.That(
            playfield.GetComponent<CharacterRig>().Runtime.Additives[0].Clip.Id,
            Is.EqualTo(CharacterAttackSystem.HighClip));
    }

    private static AnimatorRuntime AttackTestRig()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone { Id = "root", Parent = -1, Rest = Transform2D.Identity });
        package.Clips.Add(new XafClip { Id = "idle", Duration = 1f, Loop = true, LoopEnd = 1f });
        package.Clips.Add(new XafClip { Id = CharacterAttackSystem.LowClip, Duration = 0.2f });
        package.Clips.Add(new XafClip { Id = CharacterAttackSystem.MidClip, Duration = 0.2f });
        package.Clips.Add(new XafClip { Id = CharacterAttackSystem.HighClip, Duration = 0.2f });
        var runtime = new AnimatorRuntime(package);
        runtime.Play("idle", 0f);
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
        Press(KeybindAction.M);

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
        Press(KeybindAction.M);

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
            Is.EqualTo(PlayfieldRenderer.ColorFor(KeybindAction.M)));
        Assert.That(_store.Query<Note>().Count, Is.Zero);
    }

    [Test]
    public void AMissedNoteLeavesNoPop()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        CreateNote(1_000, new Vector2(500, 500));
        SetPlayhead(playfield, 1_151);
        Press(KeybindAction.M);

        Frame();

        Assert.That(_store.Query<NotePop>().Count, Is.Zero);
        Assert.That(
            playfield.GetComponent<ScoreState>().LastJudgement,
            Is.EqualTo(Judgement.Miss));
    }

    [Test]
    public void ContactBarrierSpendsBothSideCostsAutomatically()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfield.AddComponent(new ContactCounters
        {
            Left = 3,
            Right = 2,
        });
        CreateNote(
            1_000,
            new Vector2(500, 500),
            kind: NoteKind.Contact,
            contactRequired: 0,
            contactLeft: 2,
            contactRight: 1);
        SetPlayhead(playfield, 1_000);

        Frame();

        ContactCounters counters = playfield.GetComponent<ContactCounters>();
        Assert.That(counters.Left, Is.EqualTo(1));
        Assert.That(counters.Right, Is.EqualTo(1));
        Assert.That(_store.Query<Note>().Count, Is.Zero);
        Assert.That(
            playfield.GetComponent<ScoreState>().AccuracyPenalty,
            Is.Zero);
    }

    [Test]
    public void ContactBarrierFailsShortAndBreaksCombo()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfield.AddComponent(new ContactCounters { Left = 1 });
        playfield.GetComponent<ScoreState>().Combo = 7;
        CreateNote(
            1_000,
            new Vector2(500, 500),
            kind: NoteKind.Contact,
            contactRequired: 0,
            contactLeft: 2,
            contactRight: 1);
        SetPlayhead(playfield, 1_000);

        Frame();

        ContactCounters counters = playfield.GetComponent<ContactCounters>();
        Assert.That(counters.Left, Is.Zero);
        Assert.That(counters.Right, Is.Zero);
        ScoreState score = playfield.GetComponent<ScoreState>();
        Assert.That(score.Combo, Is.Zero);
        Assert.That(score.LastJudgement, Is.EqualTo(Judgement.Contact));
        Assert.That(
            score.AccuracyPenalty,
            Is.EqualTo(ScoreRules.ContactAccuracyPenalty).Within(0.0001));
        Assert.That(_judgementText.GetComponent<Text>().Value,
            Is.EqualTo("CONTACT"));
    }

    [Test]
    public void ContactBarrierIsInertForOtherCharacters()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Armed));
        playfield.AddComponent(new ContactCounters { Left = 3, Right = 3 });
        Entity barrier = CreateNote(
            1_000,
            new Vector2(500, 500),
            kind: NoteKind.Contact,
            contactRequired: 0,
            contactLeft: 1,
            contactRight: 1);
        SetPlayhead(playfield, 1_000);

        Frame();

        Assert.That(barrier.HasComponent<Note>(), Is.True);
        ContactCounters counters = playfield.GetComponent<ContactCounters>();
        Assert.That(counters.Left, Is.EqualTo(3));
        Assert.That(counters.Right, Is.EqualTo(3));
    }

    [Test]
    public void ContactBarrierThatExpiresIsNotAScoredMiss()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfield.AddComponent(new ContactCounters { Left = 3, Right = 3 });
        Entity barrier = CreateNote(
            1_000,
            new Vector2(900, 900),
            kind: NoteKind.Contact,
            contactRequired: 0,
            contactLeft: 1,
            contactRight: 1);
        SetPlayhead(playfield, 1_200);

        Frame();

        Assert.That(barrier.IsNull, Is.True);
        ScoreState score = playfield.GetComponent<ScoreState>();
        Assert.That(score.MissCount, Is.Zero);
        Assert.That(score.AccuracyPenalty, Is.Zero);
        Assert.That(score.LastJudgement, Is.EqualTo(Judgement.None));
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
    public void TappingOverAMineCostsOnePercentAccuracy()
    {
        Entity playfield = CreatePlayfield(totalNotes: 10);
        ref ScoreState scoreRef = ref playfield.GetComponent<ScoreState>();
        scoreRef.AccuracyPoints = 10;
        Entity mine = _store.CreateEntity(new Mine(1_000, new Vector2(500, 500)));
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.M);

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
        Press(KeybindAction.M);

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
        Press(KeybindAction.M);

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
        Press(KeybindAction.M);

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
        Press(KeybindAction.M);

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
        Press(KeybindAction.M);

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
        Press(KeybindAction.M);

        Frame();

        Assert.That(note.HasComponent<Note>(), Is.True);
        Assert.That(playfield.GetComponent<ScoreState>().Score, Is.Zero);
    }

    [Test]
    public void ThirtySecondBeatHitsAllSevenNoteTypesPerfectly()
    {
        const int noteCount = 30;
        Entity playfield = CreatePlayfield(totalNotes: noteCount);
        for (int i = 0; i < noteCount; i++)
        {
            CreateNote(
                1_000 + i * 1_000,
                new Vector2(500, 500),
                KeybindSettings.Actions[i % KeybindSettings.Actions.Length]);
        }

        for (int i = 0; i < noteCount; i++)
        {
            SetPlayhead(playfield, 1_000 + i * 1_000);
            KeybindAction action =
                KeybindSettings.Actions[i % KeybindSettings.Actions.Length];
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
            Press(KeybindAction.M);
            Frame();
            Release(KeybindAction.M);
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
        Press(KeybindAction.M);
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
    public void CharacterSwitchNoteChangesTheActiveModule()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfield.AddComponent(new ContactCounters());
        CreateNote(
            1_000,
            new Vector2(500, 500),
            KeybindAction.M,
            NoteKind.CharacterSwitch,
            character: CharacterKind.Armed);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.M);

        Frame();

        Assert.That(
            playfield.GetComponent<PlayerCharacter>().Kind,
            Is.EqualTo(CharacterKind.Armed));
        // The contact bank is a run resource; switching away keeps it.
        Assert.That(playfield.HasComponent<ContactCounters>(), Is.True);
    }

    [Test]
    public void WeaponSwitchNoteChangesTheWeaponByThePressedSide()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Armed));
        playfield.AddComponent(new ArmedWeapon(WeaponKind.Spear));
        CreateNote(
            1_000,
            new Vector2(500, 500),
            kind: NoteKind.WeaponSwitch);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.R2);

        Frame();

        Assert.That(
            playfield.GetComponent<ArmedWeapon>().Kind,
            Is.EqualTo(WeaponKind.Axe));
        Assert.That(
            playfield.GetComponent<ScoreState>().LastJudgement,
            Is.EqualTo(Judgement.FlawlessP));
    }

    [Test]
    public void WeaponSwitchNoteIsInertForContact()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfield.AddComponent(new ContactCounters());
        Entity note = CreateNote(
            1_000,
            new Vector2(500, 500),
            kind: NoteKind.WeaponSwitch);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.L1);

        Frame();

        Assert.That(note.HasComponent<Note>(), Is.True);
        Assert.That(
            playfield.GetComponent<ScoreState>().NotesHit,
            Is.Zero);
    }

    [Test]
    public void ArmedSwingHitsEverySameKeyNoteInsideTheSpear()
    {
        Entity playfield = CreatePlayfield(totalNotes: 3);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Armed));
        playfield.AddComponent(new ArmedWeapon(WeaponKind.Spear));
        playfield.AddComponent(new CharacterFacing());
        CreateNote(1_000, new Vector2(600, 500), KeybindAction.L1);
        CreateNote(1_000, new Vector2(550, 500), KeybindAction.L1);
        Entity outside = CreateNote(
            1_000,
            new Vector2(600, 540),
            KeybindAction.L3);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.L1);

        Frame();

        Assert.That(outside.HasComponent<Note>(), Is.True);
        Assert.That(
            playfield.GetComponent<ScoreState>().NotesHit,
            Is.EqualTo(2));
        Assert.That(_store.Query<Note>().Count, Is.EqualTo(1));
    }

    [Test]
    public void ArmedSwingDoesNotHitADifferentKeyAtTheSameTime()
    {
        Entity playfield = CreatePlayfield(totalNotes: 2);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Armed));
        playfield.AddComponent(new ArmedWeapon(WeaponKind.Spear));
        playfield.AddComponent(new CharacterFacing());
        CreateNote(1_000, new Vector2(600, 500), KeybindAction.L1);
        CreateNote(1_000, new Vector2(550, 500), KeybindAction.R2);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.L1);

        Frame();

        Assert.That(
            playfield.GetComponent<ScoreState>().NotesHit,
            Is.EqualTo(1));
        var remaining = _store.Query<Note>().ToEntityList();
        Assert.That(remaining, Has.Count.EqualTo(1));
        Assert.That(
            remaining[0].GetComponent<Note>().Target,
            Is.EqualTo(KeybindAction.R2));
    }

    [Test]
    public void SpearHitClearsDownwardMomentum()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Armed));
        playfield.AddComponent(new ArmedWeapon(WeaponKind.Spear));
        playfield.AddComponent(new CharacterFacing());
        var body = _physics.CreateDynamicBox(
            new Vector2(500, 500),
            Player.DefaultSize);
        playfield.AddComponent(new PhysicsBody(body));
        body.SetLinearVelocity(new NVector2(0f, 5f));
        CreateNote(1_000, new Vector2(600, 500), KeybindAction.L1);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.L1);

        Frame();

        Assert.That(body.LinearVelocity.Y, Is.Zero);
    }

    [Test]
    public void AxeHitKnocksThePlayerBackwards()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Armed));
        playfield.AddComponent(new ArmedWeapon(WeaponKind.Axe));
        playfield.AddComponent(new CharacterFacing());
        var body = _physics.CreateDynamicBox(
            new Vector2(500, 500),
            Player.DefaultSize);
        playfield.AddComponent(new PhysicsBody(body));
        CreateNote(1_000, new Vector2(600, 500), KeybindAction.L1);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.L1);

        Frame();

        Assert.That(body.LinearVelocity.X, Is.LessThan(0f));
    }

    [Test]
    public void SuccessfulHitBuildsTheContactCounter()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new ContactCounters());
        CreateNote(1_000, new Vector2(500, 500), KeybindAction.L1);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.L1);

        Frame();

        Assert.That(
            playfield.GetComponent<ContactCounters>().Get(KeybindAction.L1),
            Is.EqualTo(1));
    }

    [Test]
    public void OldContactNoteUsesItsKeySideAsALegacyCost()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfield.AddComponent(new ContactCounters { Left = 2, Right = 1 });
        CreateNote(
            1_000,
            new Vector2(500, 500),
            KeybindAction.R2,
            NoteKind.Contact,
            contactRequired: 1);
        SetPlayhead(playfield, 1_000);

        Frame();

        ContactCounters counters = playfield.GetComponent<ContactCounters>();
        Assert.That(counters.Left, Is.EqualTo(2));
        Assert.That(counters.Right, Is.Zero);
        Assert.That(
            playfield.GetComponent<ScoreState>().AccuracyPenalty,
            Is.Zero);
    }

    [Test]
    public void PausedContactBarrierDoesNotResolve()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1, paused: true);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfield.AddComponent(new ContactCounters { Left = 1, Right = 1 });
        Entity barrier = CreateNote(
            1_000,
            new Vector2(500, 500),
            kind: NoteKind.Contact,
            contactRequired: 0,
            contactLeft: 1,
            contactRight: 1);
        SetPlayhead(playfield, 1_000);

        Frame();

        Assert.That(barrier.HasComponent<Note>(), Is.True);
        ContactCounters counters = playfield.GetComponent<ContactCounters>();
        Assert.That(counters.Left, Is.EqualTo(1));
        Assert.That(counters.Right, Is.EqualTo(1));
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

    [Test]
    public void PairNoteIsClaimedByEitherSideAndBanksThatSide()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfield.AddComponent(new ContactCounters());
        CreateNote(
            1_000,
            new Vector2(500, 500),
            KeybindAction.L2,
            NoteKind.Pair,
            pairIndex: PairIndex.Two);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.R2);

        Frame();

        Assert.That(
            playfield.GetComponent<ScoreState>().LastJudgement,
            Is.EqualTo(Judgement.FlawlessP));
        Assert.That(
            playfield.GetComponent<ContactCounters>().Get(KeybindAction.R2),
            Is.EqualTo(1));
        Assert.That(
            playfield.GetComponent<ContactCounters>().Get(KeybindAction.L2),
            Is.Zero);
    }

    [Test]
    public void PairNoteIsJudgedOnlyOnceWhenBothSidesArePressed()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfield.AddComponent(new ContactCounters());
        CreateNote(
            1_000,
            new Vector2(500, 500),
            kind: NoteKind.Pair,
            pairIndex: PairIndex.Two);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.L2);
        Press(KeybindAction.R2);

        Frame();

        Assert.That(playfield.GetComponent<ScoreState>().NotesHit, Is.EqualTo(1));
        ContactCounters counters = playfield.GetComponent<ContactCounters>();
        Assert.That(
            counters.Get(KeybindAction.L2) + counters.Get(KeybindAction.R2),
            Is.EqualTo(1));
    }

    [Test]
    public void ChannelStartAbsorbsTheSideUntilItsEndMarker()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.SplitLine));
        playfield.AddComponent(new SplitlineState());
        CreateNote(
            1_000,
            new Vector2(500, 500),
            KeybindAction.L1,
            NoteKind.Channel,
            channelSide: ChannelSide.Left);
        Entity end = CreateNote(
            2_000,
            Vector2.Zero,
            kind: NoteKind.ChannelEnd,
            channelSide: ChannelSide.Left);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.L1);

        Frame();

        SplitlineState state = playfield.GetComponent<SplitlineState>();
        Assert.That(state.LeftActive, Is.True);
        Assert.That(state.LeftEndMs, Is.EqualTo(2_000));
        Assert.That(end.IsNull, Is.True);
        Assert.That(_store.Query<Note>().Count, Is.Zero);
    }

    [Test]
    public void RailTapUsesTheAbsorbedSideKey()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.SplitLine));
        playfield.AddComponent(new SplitlineState
        {
            LeftActive = true,
            LeftEndMs = 5_000,
        });
        CreateNote(
            1_000,
            Vector2.Zero,
            kind: NoteKind.Rail,
            pairIndex: PairIndex.One,
            channelSide: ChannelSide.Left);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.L1);

        Frame();

        Assert.That(_store.Query<Note>().Count, Is.Zero);
        Assert.That(
            playfield.GetComponent<ScoreState>().LastJudgement,
            Is.EqualTo(Judgement.FlawlessP));
    }

    [Test]
    public void RailHoldStartsAHoldAndReleasingEarlyFailsIt()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.SplitLine));
        playfield.AddComponent(new SplitlineState
        {
            LeftActive = true,
            LeftEndMs = 5_000,
        });
        CreateNote(
            1_000,
            Vector2.Zero,
            kind: NoteKind.Rail,
            pairIndex: PairIndex.One,
            channelSide: ChannelSide.Left,
            holdMs: 500);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.L1);

        Frame();
        Assert.That(_store.Query<RailHold>().Count, Is.EqualTo(1));

        Release(KeybindAction.L1);
        SetPlayhead(playfield, 1_100);
        Frame();

        Assert.That(_store.Query<RailHold>().Count, Is.Zero);
        Assert.That(
            playfield.GetComponent<ScoreState>().AccuracyPenalty,
            Is.GreaterThan(0));
        Assert.That(
            playfield.GetComponent<ScoreState>().LastJudgement,
            Is.EqualTo(Judgement.Miss));
    }

    [Test]
    public void BothChannelsRemoveThePlayerUntilOneEnds()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new SplitlineState
        {
            LeftActive = true,
            RightActive = true,
            LeftEndMs = 2_000,
            RightEndMs = 5_000,
        });

        Frame();
        Assert.That(playfield.GetComponent<SplitlineState>().Absent, Is.True);
        Assert.That(playfield.Tags.Has<SplitlineAbsent>(), Is.True);

        SetPlayhead(playfield, 2_000);
        Frame();

        SplitlineState state = playfield.GetComponent<SplitlineState>();
        Assert.That(state.LeftActive, Is.False);
        Assert.That(state.Absent, Is.False);
        Assert.That(playfield.Tags.Has<SplitlineAbsent>(), Is.False);
    }

    [Test]
    public void ConstructionMarkerStartsTheShape()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Form));
        _store.CreateEntity(new Block(
            Vector2.Zero,
            new Vector2(100, 100),
            shapeId: 5,
            shapePiece: 0)).AddTag<ConstructionHidden>();
        _store.CreateEntity(new Block(
            new Vector2(200, 0),
            new Vector2(100, 100),
            shapeId: 5,
            shapePiece: 1)).AddTag<ConstructionHidden>();
        CreateNote(
            1_000,
            new Vector2(500, 500),
            KeybindAction.L1,
            NoteKind.Construction,
            shapeId: 5);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.L1);

        Frame();

        ConstructionState state = playfield.GetComponent<ConstructionState>();
        Assert.That(state.Active, Is.True);
        Assert.That(state.ShapeId, Is.EqualTo(5));
        Assert.That(state.TotalPieces, Is.EqualTo(2));
    }

    [Test]
    public void ConstructionStepRevealsItsBlockAndCompletesTheShape()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Form));
        playfield.AddComponent(new ConstructionState
        {
            Active = true,
            ShapeId = 5,
            TotalPieces = 2,
        });
        Entity block = _store.CreateEntity(new Block(
            Vector2.Zero,
            new Vector2(100, 100),
            shapeId: 5,
            shapePiece: 0));
        block.AddTag<ConstructionHidden>();
        CreateNote(
            1_000,
            new Vector2(500, 500),
            KeybindAction.L1,
            NoteKind.ConstructionStep,
            shapeId: 5,
            shapePiece: 0);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.L1);

        Frame();

        Assert.That(block.Tags.Has<ConstructionHidden>(), Is.False);
        ConstructionState state = playfield.GetComponent<ConstructionState>();
        Assert.That(state.BuiltPieces, Is.EqualTo(1));
        Assert.That(_store.Query<Note>().Count, Is.Zero);
    }

    [Test]
    public void ConstructionStepIsInertWithoutAnActiveShape()
    {
        Entity playfield = CreatePlayfield(totalNotes: 1);
        playfield.AddComponent(new PlayerCharacter(CharacterKind.Form));
        playfield.AddComponent(new ConstructionState { ShapeId = 5 });
        Entity step = CreateNote(
            1_000,
            new Vector2(500, 500),
            KeybindAction.L1,
            NoteKind.ConstructionStep,
            shapeId: 5,
            shapePiece: 0);
        SetPlayhead(playfield, 1_000);
        Press(KeybindAction.L1);

        Frame();

        Assert.That(step.HasComponent<Note>(), Is.True);
        Assert.That(playfield.GetComponent<ScoreState>().NotesHit, Is.Zero);
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
        KeybindAction target = KeybindAction.M,
        NoteKind kind = NoteKind.Hit,
        int contactRequired = 1,
        CharacterKind character = CharacterKind.Contact,
        PairIndex pairIndex = PairIndex.One,
        float momentum = Note.DefaultMomentum,
        int contactLeft = 0,
        int contactRight = 0,
        ChannelSide channelSide = ChannelSide.Left,
        long holdMs = 0,
        int shapeId = 0,
        int shapePiece = 0) =>
        _store.CreateEntity(new Note(
            timeMs,
            position,
            target: target,
            kind: kind,
            contactRequired: contactRequired,
            character: character,
            pairIndex: pairIndex,
            momentum: momentum,
            contactLeft: contactLeft,
            contactRight: contactRight,
            channelSide: channelSide,
            holdMs: holdMs,
            shapeId: shapeId,
            shapePiece: shapePiece));

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
