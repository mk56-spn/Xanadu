using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Editor;
using Xanadu.Settings;
using Xanadu.Systems.Input;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorNoteInputSystemTests
{
    private EntityStore _store = null!;
    private ScriptedPointer _pointer = null!;
    private EditorHistory _history = null!;
    private SystemRoot _root = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _pointer = new ScriptedPointer();
        _history = new EditorHistory(_store);
        _root = new SystemRoot(_store);
        _root.Add(new EditorNoteInputSystem(_store, _pointer, _history));

        _store.CreateEntity(
            new LevelInfo("Test", "/tmp/test.wav"),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, false),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000)),
            new NoteSelection(default));
    }

    [Test]
    public void ClickingATimelineNoteSelectsIt()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.DurationMs = 10_000;
        playhead.TimeMs = 5_000;
        editor.AddComponent(new TimelineView());

        Entity note = _store.CreateEntity(new Note(5_000, new Vector2(500, 500)));
        Rect2 ruler = EditorLayout.RulerRect(
            editor.GetComponent<PlayfieldView>());
        Vector2 point = ruler.Position + ruler.Size / 2;

        _pointer.Current = new PointerInput(point, true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(editor.GetComponent<NoteSelection>().Note.Id,
            Is.EqualTo(note.Id));
    }

    [Test]
    public void DraggingATimelineNoteRetimesIt()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new TimelineView(2f));
        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.DurationMs = 10_000;
        playhead.TimeMs = 2_500;

        Entity note = _store.CreateEntity(
            new Note(2_500, new Vector2(500, 500)));
        Rect2 ruler = EditorLayout.RulerRect(
            editor.GetComponent<PlayfieldView>());
        Vector2 start = new(
            ruler.Position.X + ruler.Size.X * 0.5f,
            ruler.Position.Y + ruler.Size.Y / 2);
        Vector2 end = start + new Vector2(ruler.Size.X * 0.2f, 0);

        _pointer.Current = new PointerInput(start, true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(end, true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(note.GetComponent<Note>().TimeMs, Is.EqualTo(3_500));
        Assert.That(playhead.SeekRequestMs, Is.EqualTo(3_500));
    }

    [Test]
    public void TimelineDragRetimesTheWholeMixedSelection()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new TimelineView(2f));
        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.DurationMs = 10_000;
        playhead.TimeMs = 2_500;

        Entity note = _store.CreateEntity(
            new Note(2_500, new Vector2(500, 500)));
        Entity mine = _store.CreateEntity(
            new Mine(2_000, new Vector2(100, 100)));
        Entity start = _store.CreateEntity(new StartPoint(
            "Start", 3_000, new Vector2(200, 200),
            System.Numerics.Vector2.Zero, new Vector2(0, -1)));
        editor.GetComponent<NoteSelection>().SetMany(new[] { note, mine, start });

        Rect2 ruler = EditorLayout.RulerRect(
            editor.GetComponent<PlayfieldView>());
        Vector2 startPoint = new(
            ruler.Position.X + ruler.Size.X * 0.5f,
            ruler.Position.Y + ruler.Size.Y / 2);
        Vector2 endPoint = startPoint + new Vector2(ruler.Size.X * 0.2f, 0);

        _pointer.Current = new PointerInput(startPoint, true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(endPoint, true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(note.GetComponent<Note>().TimeMs, Is.EqualTo(3_500));
        Assert.That(mine.GetComponent<Mine>().TimeMs, Is.EqualTo(3_000));
        Assert.That(start.GetComponent<StartPoint>().TimeMs, Is.EqualTo(4_000));
    }

    [Test]
    public void TimelineRetimeIsUndoable()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new TimelineView(2f));
        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.DurationMs = 10_000;
        playhead.TimeMs = 2_500;

        Entity note = _store.CreateEntity(
            new Note(2_500, new Vector2(500, 500)));
        Rect2 ruler = EditorLayout.RulerRect(
            editor.GetComponent<PlayfieldView>());
        Vector2 start = new(
            ruler.Position.X + ruler.Size.X * 0.5f,
            ruler.Position.Y + ruler.Size.Y / 2);

        _pointer.Current = new PointerInput(start, true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(
            start + new Vector2(ruler.Size.X * 0.2f, 0), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(
            start + new Vector2(ruler.Size.X * 0.2f, 0), false);
        _root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(note.GetComponent<Note>().TimeMs, Is.EqualTo(3_500));

        Assert.That(_history.Undo(), Is.True);
        Assert.That(
            _store.Query<Note>().ToEntityList()[0].GetComponent<Note>().TimeMs,
            Is.EqualTo(2_500));
    }

    [Test]
    public void ContactModePlacesContactNotes()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new EditorPlacementModeState(
            EditorPlacementMode.Contact));
        editor.AddComponent(new EditorContactCostState(2, 0));

        _pointer.Current = new PointerInput(new Vector2(300, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(300, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Note note = _store.Query<Note>().ToEntityList()[0].GetComponent<Note>();
        Assert.That(note.Kind, Is.EqualTo(NoteKind.Contact));
        Assert.That(note.ContactLeft, Is.EqualTo(2));
        Assert.That(note.ContactRight, Is.Zero);
        Assert.That(note.Target, Is.EqualTo(KeybindAction.M));
    }

    [Test]
    public void CharacterSwitchModePlacesTheSelectedCharacter()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new EditorPlacementModeState(
            EditorPlacementMode.CharacterSwitch));
        editor.AddComponent(new EditorCharacterTarget(CharacterKind.Armed));

        _pointer.Current = new PointerInput(new Vector2(300, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(300, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Note note = _store.Query<Note>().ToEntityList()[0].GetComponent<Note>();
        Assert.That(note.Kind, Is.EqualTo(NoteKind.CharacterSwitch));
        Assert.That(note.Target, Is.EqualTo(KeybindAction.M));
        Assert.That(note.Character, Is.EqualTo(CharacterKind.Armed));
    }

    [Test]
    public void WeaponSwitchModePlacesAWeaponSwitchNote()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new EditorPlacementModeState(
            EditorPlacementMode.WeaponSwitch));

        _pointer.Current = new PointerInput(new Vector2(300, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(300, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Note note = _store.Query<Note>().ToEntityList()[0].GetComponent<Note>();
        Assert.That(note.Kind, Is.EqualTo(NoteKind.WeaponSwitch));
        Assert.That(note.Target, Is.EqualTo(KeybindAction.M));
    }

    [Test]
    public void ChannelModePlacesAChannelStartOnTheSelectedSide()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new EditorPlacementModeState(
            EditorPlacementMode.Channel));
        editor.AddComponent(new EditorNoteTarget(KeybindAction.R2));

        _pointer.Current = new PointerInput(new Vector2(300, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(300, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Note note = _store.Query<Note>().ToEntityList()[0].GetComponent<Note>();
        Assert.That(note.Kind, Is.EqualTo(NoteKind.Channel));
        Assert.That(note.Target, Is.EqualTo(KeybindAction.R2));
        Assert.That(note.ChannelSide, Is.EqualTo(ChannelSide.Right));
    }

    [Test]
    public void FormStepModePlacesAStepForTheActiveShape()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new EditorPlacementModeState(
            EditorPlacementMode.FormStep));
        editor.AddComponent(new EditorNoteTarget(KeybindAction.R3));
        editor.AddComponent(new EditorShapeState(7));

        _pointer.Current = new PointerInput(new Vector2(300, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(300, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Note note = _store.Query<Note>().ToEntityList()[0].GetComponent<Note>();
        Assert.That(note.Kind, Is.EqualTo(NoteKind.ConstructionStep));
        Assert.That(note.Target, Is.EqualTo(KeybindAction.R3));
        Assert.That(note.ShapeId, Is.EqualTo(7));
        Assert.That(note.ShapePiece, Is.Zero);
    }

    [Test]
    public void ShiftClickOnTimelineAddsToTheCurrentSelection()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.DurationMs = 10_000;
        playhead.TimeMs = 5_000;
        editor.AddComponent(new TimelineView());

        Entity first = _store.CreateEntity(
            new Note(3_000, new Vector2(500, 500)));
        Entity second = _store.CreateEntity(
            new Note(7_000, new Vector2(500, 500)));
        Rect2 ruler = EditorLayout.RulerRect(
            editor.GetComponent<PlayfieldView>());
        float y = ruler.Position.Y + ruler.Size.Y / 2;

        _pointer.Current = new PointerInput(
            new Vector2(ruler.Position.X + ruler.Size.X * 0.3f, y), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(
            new Vector2(ruler.Position.X + ruler.Size.X * 0.3f, y), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        _pointer.Current = new PointerInput(
            new Vector2(ruler.Position.X + ruler.Size.X * 0.7f, y), true, true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        NoteSelection selection = editor.GetComponent<NoteSelection>();
        Assert.That(selection.AllNotes.Count, Is.EqualTo(2));
        Assert.That(selection.Contains(first), Is.True);
        Assert.That(selection.Contains(second), Is.True);
    }

    [Test]
    public void ShiftClickOnPlayfieldKeepsExistingSelection()
    {
        Entity first = _store.CreateEntity(
            new Note(1_000, new Vector2(100, 200)));
        Entity second = _store.CreateEntity(
            new Note(1_000, new Vector2(400, 400)));
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];

        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        _pointer.Current = new PointerInput(new Vector2(400, 400), true, true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        NoteSelection selection = editor.GetComponent<NoteSelection>();
        Assert.That(selection.AllNotes.Count, Is.EqualTo(2));
        Assert.That(selection.Contains(first), Is.True);
        Assert.That(selection.Contains(second), Is.True);
    }

    [Test]
    public void ClickWithoutSongRequestsWarningAndSkipsNote()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        ref LevelInfo level = ref editor.GetComponent<LevelInfo>();
        level.AudioPath = "";

        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_store.Query<Note>().Count, Is.EqualTo(0));
        Assert.That(editor.HasComponent<WarningRequest>(), Is.True);
        Assert.That(
            editor.GetComponent<WarningRequest>().Message,
            Is.EqualTo(EditorWarningPopup.NoSongMessage));
    }

    [Test]
    public void ClickAddsSnappedNoteAndSelectsIt()
    {
        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        var notes = _store.Query<Note>().ToEntityList();
        Assert.That(notes.Count, Is.EqualTo(1));
        Assert.That(notes[0].GetComponent<Note>().TimeMs, Is.EqualTo(1_000));
        Assert.That(notes[0].GetComponent<Note>().Position, Is.EqualTo(new Vector2(100, 200)));

        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        Assert.That(editor.GetComponent<NoteSelection>().Note.Id, Is.EqualTo(notes[0].Id));
    }

    [Test]
    public void NewNotesKeepTheSelectedTarget()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new EditorNoteTarget(KeybindAction.R3));

        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        editor.GetComponent<NoteSelection>().Note = default;

        _pointer.Current = new PointerInput(new Vector2(700, 700), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(700, 700), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        var notes = _store.Query<Note>().ToEntityList();
        Assert.That(notes.Count, Is.EqualTo(2));
        Assert.That(
            notes[0].GetComponent<Note>().Target,
            Is.EqualTo(KeybindAction.R3));
        Assert.That(
            notes[1].GetComponent<Note>().Target,
            Is.EqualTo(KeybindAction.R3));
    }

    [Test]
    public void TimelineScrubOnlyPansOutsideTheCenterDeadZone()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.DurationMs = 10_000;
        playhead.TimeMs = 1_000;
        editor.AddComponent(new TimelineView(2f));

        Rect2 ruler = EditorLayout.RulerRect(
            editor.GetComponent<PlayfieldView>());
        float centerX = ruler.Position.X + ruler.Size.X / 2;
        float y = ruler.Position.Y + ruler.Size.Y / 2;

        _pointer.Current = new PointerInput(
            new Vector2(centerX + ruler.Size.X * 0.2f, y), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        long before = editor.GetComponent<TimelineView>().ScrubStartMs;

        _pointer.Current = new PointerInput(
            new Vector2(centerX + ruler.Size.X * 0.25f, y), true);
        for (int i = 0; i < 10; i++)
            _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            editor.GetComponent<TimelineView>().ScrubStartMs,
            Is.EqualTo(before));

        _pointer.Current = new PointerInput(
            new Vector2(centerX + ruler.Size.X * 0.9f, y), true);
        for (int i = 0; i < 10; i++)
            _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            editor.GetComponent<TimelineView>().ScrubStartMs,
            Is.GreaterThan(before));
    }

    [Test]
    public void DraggingTheTimelineSeeksContinuously()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.DurationMs = 10_000;
        playhead.TimeMs = 1_000;

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Rect2 ruler = EditorLayout.RulerRect(view);
        _pointer.Current = new PointerInput(ruler.GetCenter(), true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(playhead.SeekRequestMs, Is.EqualTo(5_000));
    }

    [Test]
    public void MineModePlacesMinesInsteadOfNotes()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new EditorPlacementModeState(EditorPlacementMode.Mines));

        _pointer.Current = new PointerInput(new Vector2(300, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(300, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_store.Query<Note>().Count, Is.Zero);
        var mines = _store.Query<Mine>().ToEntityList();
        Assert.That(mines, Has.Count.EqualTo(1));
        Assert.That(mines[0].GetComponent<Mine>().TimeMs, Is.EqualTo(1_000));
        Assert.That(
            mines[0].GetComponent<Mine>().Position,
            Is.EqualTo(new Vector2(300, 400)));
        Assert.That(_history.CanUndo, Is.True);
    }

    [Test]
    public void MovementModePlacesADirectionalMovementNote()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(
            new EditorPlacementModeState(EditorPlacementMode.Dash));
        editor.AddComponent(
            new EditorDashDirectionState(DashDirection.UpRight));
        editor.AddComponent(
            new EditorDashStrengthState(DashStrength.Double));

        _pointer.Current = new PointerInput(new Vector2(300, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(300, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        var notes = _store.Query<Note>().ToEntityList();
        Assert.That(notes, Has.Count.EqualTo(1));
        Assert.That(notes[0].GetComponent<Note>().Kind,
            Is.EqualTo(NoteKind.Dash));
        Assert.That(notes[0].GetComponent<Note>().DashDirection,
            Is.EqualTo(DashDirection.UpRight));
        Assert.That(notes[0].GetComponent<Note>().DashStrength,
            Is.EqualTo(DashStrength.Double));
        Assert.That(_store.Query<Mine>().Count, Is.Zero);
    }

    [Test]
    public void PairModePlacesTheNumberedPairFromTheSelectedKey()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(
            new EditorPlacementModeState(EditorPlacementMode.Pair));
        editor.AddComponent(new EditorNoteTarget(KeybindAction.R2));

        _pointer.Current = new PointerInput(new Vector2(300, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(300, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        var notes = _store.Query<Note>().ToEntityList();
        Assert.That(notes, Has.Count.EqualTo(1));
        Note note = notes[0].GetComponent<Note>();
        Assert.That(note.Kind, Is.EqualTo(NoteKind.Pair));
        Assert.That(note.PairIndex, Is.EqualTo(PairIndex.Two));
        // Pair notes do not store a side.
        Assert.That(note.Target, Is.EqualTo(KeybindAction.M));
    }

    [Test]
    public void MomentumModePlacesTheAuthoredDirectionAndStrength()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(
            new EditorPlacementModeState(EditorPlacementMode.Momentum));
        editor.AddComponent(
            new EditorDashDirectionState(DashDirection.Left));
        editor.AddComponent(
            new EditorDashStrengthState(DashStrength.Double));

        _pointer.Current = new PointerInput(new Vector2(300, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(300, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        var notes = _store.Query<Note>().ToEntityList();
        Assert.That(notes, Has.Count.EqualTo(1));
        Note note = notes[0].GetComponent<Note>();
        Assert.That(note.Kind, Is.EqualTo(NoteKind.Momentum));
        Assert.That(note.DashDirection, Is.EqualTo(DashDirection.Left));
        Assert.That(note.Momentum, Is.EqualTo(900f));
    }

    [Test]
    public void UndoRemovesAPlacedMine()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new EditorPlacementModeState(EditorPlacementMode.Mines));

        _pointer.Current = new PointerInput(new Vector2(300, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(300, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(_history.Undo(), Is.True);

        Assert.That(_store.Query<Mine>().Count, Is.Zero);
    }

    [Test]
    public void SpikeModePaintsOneTilePerGridCell()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new EditorPlacementModeState(EditorPlacementMode.Spikes));

        _pointer.Current = new PointerInput(new Vector2(350, 640), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(450, 640), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(350, 740), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(350, 740), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        var spikes = _store.Query<Spike>().ToEntityList();
        Assert.That(spikes, Has.Count.EqualTo(3));
        Assert.That(
            spikes[0].GetComponent<Spike>().Position,
            Is.EqualTo(new Vector2(350, 600)));
        Assert.That(
            spikes[0].GetComponent<Spike>().Size,
            Is.EqualTo(new Vector2(50, 50)));
        Assert.That(
            spikes[1].GetComponent<Spike>().Position,
            Is.EqualTo(new Vector2(450, 600)));
        Assert.That(
            spikes[2].GetComponent<Spike>().Position,
            Is.EqualTo(new Vector2(350, 700)));
        Assert.That(_store.Query<Note>().Count, Is.Zero);
    }

    [Test]
    public void SpikePaintingSkipsCellThatAlreadyHasATile()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new EditorPlacementModeState(EditorPlacementMode.Spikes));
        _store.CreateEntity(
            new Spike(new Vector2(400, 600), new Vector2(100, 100)));

        _pointer.Current = new PointerInput(new Vector2(350, 640), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(450, 640), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(450, 640), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_store.Query<Spike>().Count, Is.EqualTo(2));
    }

    [Test]
    public void UndoRemovesAWholeSpikeStroke()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new EditorPlacementModeState(EditorPlacementMode.Spikes));

        _pointer.Current = new PointerInput(new Vector2(350, 640), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(450, 640), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(550, 640), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(550, 640), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_store.Query<Spike>().Count, Is.EqualTo(3));
        Assert.That(_history.Undo(), Is.True);
        Assert.That(_store.Query<Spike>().Count, Is.Zero);
    }

    [Test]
    public void DraggingASpikeSnapsItToTheGrid()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        Entity spike = _store.CreateEntity(
            new Spike(new Vector2(300, 600), new Vector2(100, 100)),
            Tags.Get<EditorItem>());

        _pointer.Current = new PointerInput(new Vector2(350, 650), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(490, 685), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(490, 685), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            spike.GetComponent<Spike>().Position,
            Is.EqualTo(new Vector2(400, 600)));
    }

    [Test]
    public void SidebarClicksDoNotPlaceNotes()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new EditorSidebar(new Rect2(800, 50, 200, 500)));

        _pointer.Current = new PointerInput(new Vector2(900, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_store.Query<Note>().Count, Is.Zero);
    }

    [Test]
    public void ClickingExistingNoteSelectsWithoutAdding()
    {
        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.GetComponent<NoteSelection>().Note = default;

        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_store.Query<Note>().Count, Is.EqualTo(1));
        Assert.That(editor.GetComponent<NoteSelection>().Note.IsNull, Is.False);
    }

    [Test]
    public void InvisibleNoteDoesNotCaptureClick()
    {
        _store.CreateEntity(new Note(5_000, new Vector2(100, 200)));
        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        var notes = _store.Query<Note>().ToEntityList();
        Assert.That(notes.Count, Is.EqualTo(2));
    }

    [Test]
    public void DraggingEmptySpaceSelectsWithoutCreating()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        _store.CreateEntity(new Note(1_000, new Vector2(200, 200)));
        _store.CreateEntity(new Note(1_000, new Vector2(300, 300)));
        _store.CreateEntity(new Note(1_000, new Vector2(700, 700)));

        _pointer.Current = new PointerInput(new Vector2(50, 50), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(400, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_store.Query<Note>().Count, Is.EqualTo(3));
        Assert.That(editor.GetComponent<NoteSelection>().Notes, Has.Count.EqualTo(2));

        _pointer.Current = new PointerInput(new Vector2(400, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(_store.Query<Note>().Count, Is.EqualTo(3));
    }

    [Test]
    public void MarqueeSkipsInvisibleNotes()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        _store.CreateEntity(new Note(1_000, new Vector2(200, 200)));
        _store.CreateEntity(new Note(5_000, new Vector2(300, 300)));

        _pointer.Current = new PointerInput(new Vector2(50, 50), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(400, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            editor.GetComponent<NoteSelection>().Notes,
            Has.Count.EqualTo(1));
    }

    [Test]
    public void ShiftDraggingABoxAddsToTheExistingSelection()
    {
        Entity first = _store.CreateEntity(new Note(1_000, new Vector2(100, 200)));
        Entity second = _store.CreateEntity(new Note(1_000, new Vector2(400, 400)));
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];

        // Select the first note, then shift-drag a box over the second from empty space.
        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(700, 700), true, true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(350, 350), true, true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(350, 350), false, true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        NoteSelection selection = editor.GetComponent<NoteSelection>();
        Assert.That(selection.Contains(first), Is.True);
        Assert.That(selection.Contains(second), Is.True);
        Assert.That(_store.Query<Note>().Count, Is.EqualTo(2));
    }

    [Test]
    public void ShiftDraggingAcrossTheTimelineSelectsTheNotesUnderTheBox()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.DurationMs = 10_000;
        playhead.TimeMs = 5_000;
        editor.AddComponent(new TimelineView());
        editor.AddComponent(new UiMarqueeBox());

        Entity covered = _store.CreateEntity(new Note(5_000, new Vector2(500, 500)));
        Entity outside = _store.CreateEntity(new Note(7_000, new Vector2(500, 500)));
        Entity kept = _store.CreateEntity(new Note(3_000, new Vector2(500, 500)));
        editor.GetComponent<NoteSelection>().Note = kept;

        Rect2 ruler = EditorLayout.RulerRect(editor.GetComponent<PlayfieldView>());
        (long start, long end) = EditorLayout.TimelineWindow(playhead, 1f);
        Rect2 slot = default;
        foreach ((Entity note, Rect2 bounds) in EditorTimelineNotes.Slots(
                     _store, ruler, start, end, 1f))
        {
            if (note.Id == covered.Id)
                slot = bounds;
        }

        Vector2 from = new(slot.Position.X - 4, ruler.Position.Y + 2);
        Vector2 to = new(slot.End.X + 1, ruler.End.Y - 2);

        _pointer.Current = new PointerInput(from, true, true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(to, true, true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(editor.GetComponent<UiMarqueeBox>().Active, Is.True);

        _pointer.Current = new PointerInput(to, false, true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        NoteSelection selection = editor.GetComponent<NoteSelection>();
        Assert.That(selection.Contains(covered), Is.True);
        Assert.That(selection.Contains(kept), Is.True);
        Assert.That(selection.Contains(outside), Is.False);
        Assert.That(editor.GetComponent<UiMarqueeBox>().Active, Is.False);
        Assert.That(editor.GetComponent<TimelineView>().Scrubbing, Is.False);
    }

    [Test]
    public void ADraggedBoxIsPublishedForDrawingAndClearedOnRelease()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new UiMarqueeBox());

        _pointer.Current = new PointerInput(new Vector2(50, 60), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(250, 160), true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        UiMarqueeBox box = editor.GetComponent<UiMarqueeBox>();
        Assert.That(box.Active, Is.True);
        Assert.That(box.Bounds, Is.EqualTo(new Rect2(50, 60, 200, 100)));

        _pointer.Current = new PointerInput(new Vector2(250, 160), false);
        _root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(editor.GetComponent<UiMarqueeBox>().Active, Is.False);
    }

    [Test]
    public void MineModeMarqueeSelectsWithoutPlacingAMine()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(
            new EditorPlacementModeState(EditorPlacementMode.Mines));
        _store.CreateEntity(new Note(1_000, new Vector2(200, 200)));
        _store.CreateEntity(new Note(1_000, new Vector2(300, 300)));

        _pointer.Current = new PointerInput(new Vector2(50, 50), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(400, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(400, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(editor.GetComponent<NoteSelection>().Notes, Has.Count.EqualTo(2));
        Assert.That(_store.Query<Mine>().Count, Is.Zero);
    }

    [Test]
    public void MovementModeMarqueeSelectsNotes()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(
            new EditorPlacementModeState(EditorPlacementMode.Dash));
        _store.CreateEntity(new Note(1_000, new Vector2(200, 200)));
        _store.CreateEntity(new Note(1_000, new Vector2(300, 300)));

        _pointer.Current = new PointerInput(new Vector2(50, 50), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(400, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(400, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(editor.GetComponent<NoteSelection>().Notes, Has.Count.EqualTo(2));
        Assert.That(_store.Query<Note>().Count, Is.EqualTo(2));
    }

    [Test]
    public void DraggingAMultiSelectionMovesAllSelectedNotes()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        Entity first = _store.CreateEntity(new Note(1_000, new Vector2(200, 200)));
        Entity second = _store.CreateEntity(new Note(1_000, new Vector2(300, 300)));
        editor.GetComponent<NoteSelection>().SetMany(new[] { first, second });

        _pointer.Current = new PointerInput(new Vector2(200, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(250, 250), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(250, 250), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(editor.GetComponent<NoteSelection>().Notes, Has.Count.EqualTo(2));
        Assert.That(first.GetComponent<Note>().Position, Is.EqualTo(new Vector2(250, 250)));
        Assert.That(second.GetComponent<Note>().Position, Is.EqualTo(new Vector2(350, 350)));
    }

    [Test]
    public void DraggingAMineMovesIt()
    {
        Entity mine = _store.CreateEntity(
            new Mine(1_000, new Vector2(200, 200)));

        _pointer.Current = new PointerInput(new Vector2(200, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(260, 270), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(260, 270), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(mine.GetComponent<Mine>().Position,
            Is.EqualTo(new Vector2(260, 270)));
    }

    [Test]
    public void StartModePlacesAPracticeStart()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(
            new EditorPlacementModeState(EditorPlacementMode.Start));

        _pointer.Current = new PointerInput(new Vector2(300, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(300, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        var starts = _store.Query<StartPoint>().ToEntityList();
        Assert.That(starts, Has.Count.EqualTo(1));
        Assert.That(starts[0].GetComponent<StartPoint>().TimeMs,
            Is.EqualTo(1_000));
        Assert.That(starts[0].GetComponent<StartPoint>().Position,
            Is.EqualTo(new Vector2(300, 400)));
    }

    [Test]
    public void DraggingAStartPointMovesIt()
    {
        Entity start = _store.CreateEntity(new StartPoint(
            "",
            1_000,
            new Vector2(200, 200),
            System.Numerics.Vector2.Zero,
            new Vector2(0, -1)));

        _pointer.Current = new PointerInput(new Vector2(200, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(260, 270), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(260, 270), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(start.GetComponent<StartPoint>().Position,
            Is.EqualTo(new Vector2(260, 270)));
    }

    [Test]
    public void DeletedDraggedNoteDoesNotThrow()
    {
        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        var notes = _store.Query<Note>().ToEntityList();
        notes[0].DeleteEntity();

        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        Assert.DoesNotThrow(() =>
            _root.Update(new UpdateTick(1f / 60f, 0)));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        Assert.DoesNotThrow(() =>
            _root.Update(new UpdateTick(1f / 60f, 0)));
    }


    [Test]
    public void BlockModePlacesAGridAlignedBlock()
    {
        Entity editor = EditorEntity();
        editor.AddComponent(
            new EditorPlacementModeState(EditorPlacementMode.Blocks));

        Click(new Vector2(520, 520));

        var blocks = _store.Query<Block>().ToEntityList();
        Assert.That(blocks, Has.Count.EqualTo(1));
        Assert.That(
            blocks[0].GetComponent<Block>().Position,
            Is.EqualTo(new Vector2(450, 500)));
        Assert.That(
            blocks[0].GetComponent<Block>().Size,
            Is.EqualTo(new Vector2(100, 50)));
        Assert.That(
            editor.GetComponent<NoteSelection>().Note.Id,
            Is.EqualTo(blocks[0].Id));
        Assert.That(_history.CanUndo, Is.True);
    }

    [Test]
    public void ClickingABlockSelectsIt()
    {
        Entity block = _store.CreateEntity(
            new Block(new Vector2(400, 500), new Vector2(200, 100)));
        Entity editor = EditorEntity();

        Click(new Vector2(450, 520));

        Assert.That(
            editor.GetComponent<NoteSelection>().Note.Id,
            Is.EqualTo(block.Id));
    }

    [Test]
    public void DraggingABlockSnapsItsEdgesToTheGrid()
    {
        Entity block = _store.CreateEntity(
            new Block(new Vector2(400, 500), new Vector2(200, 100)));

        Drag(new Vector2(500, 550), new Vector2(560, 590));

        Assert.That(
            block.GetComponent<Block>().Position,
            Is.EqualTo(new Vector2(450, 550)));
    }

    [Test]
    public void DraggingABlockBeyondTheFieldStopsAtTheEdge()
    {
        Entity block = _store.CreateEntity(
            new Block(new Vector2(400, 500), new Vector2(200, 100)));

        Drag(new Vector2(500, 550), new Vector2(1200, 550));

        Assert.That(
            block.GetComponent<Block>().Position,
            Is.EqualTo(new Vector2(800, 500)));
    }

    [Test]
    public void DraggingABlockIsUndoable()
    {
        _store.CreateEntity(
            new Block(new Vector2(400, 500), new Vector2(200, 100)));

        Drag(new Vector2(500, 550), new Vector2(560, 590));
        Assert.That(_history.Undo(), Is.True);

        Assert.That(
            _store.Query<Block>().ToEntityList()[0].GetComponent<Block>().Position,
            Is.EqualTo(new Vector2(400, 500)));
    }

    [Test]
    public void DraggingTheRightNubResizesTheBlock()
    {
        Entity block = SelectedPlatform();

        Drag(new Vector2(600, 550), new Vector2(688, 550));

        Assert.That(
            block.GetComponent<Block>().Size,
            Is.EqualTo(new Vector2(300, 100)));
        Assert.That(
            block.GetComponent<Block>().Position,
            Is.EqualTo(new Vector2(400, 500)));
    }

    [Test]
    public void DraggingTheTopNubResizesTheBlock()
    {
        Entity block = SelectedPlatform();

        Drag(new Vector2(500, 500), new Vector2(500, 380));

        Assert.That(
            block.GetComponent<Block>().Position,
            Is.EqualTo(new Vector2(400, 400)));
        Assert.That(
            block.GetComponent<Block>().Size,
            Is.EqualTo(new Vector2(200, 200)));
    }

    [Test]
    public void ANubCannotShrinkABlockBelowOneCell()
    {
        Entity block = SelectedPlatform();

        Drag(new Vector2(600, 550), new Vector2(420, 550));

        Assert.That(
            block.GetComponent<Block>().Size.X,
            Is.EqualTo(BlockGrid.Cell(new Playfield(1000, 1000)).X));
    }

    [Test]
    public void ResizingABlockIsUndoable()
    {
        SelectedPlatform();

        Drag(new Vector2(600, 550), new Vector2(688, 550));
        Assert.That(_history.Undo(), Is.True);

        Assert.That(
            _store.Query<Block>().ToEntityList()[0].GetComponent<Block>().Size,
            Is.EqualTo(new Vector2(200, 100)));
    }

    private Entity SelectedPlatform()
    {
        Entity block = _store.CreateEntity(
            new Block(new Vector2(400, 500), new Vector2(200, 100)));
        EditorEntity().GetComponent<NoteSelection>().Note = block;
        return block;
    }

    private Entity EditorEntity() =>
        _store.Query<PlayfieldView>().ToEntityList()[0];

    private void Click(Vector2 point)
    {
        _pointer.Current = new PointerInput(point, true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(point, false);
        _root.Update(new UpdateTick(1f / 60f, 0));
    }

    private void Drag(Vector2 from, Vector2 to)
    {
        _pointer.Current = new PointerInput(from, true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(to, true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(to, false);
        _root.Update(new UpdateTick(1f / 60f, 0));
    }

    private sealed class ScriptedPointer : IPointerInputSource
    {
        public PointerInput Current;

        public PointerInput Read() => Current;
    }
}
