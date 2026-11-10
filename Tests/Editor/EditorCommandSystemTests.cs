using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Tests.Support;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Settings;
using Xanadu.Systems.Editor;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorCommandSystemTests : EcsSystemTest
{
    private EditorCommandBus _commands = null!;
    private Entity _editor;

    protected override void OnEcsSetUp()
    {
        _commands = new EditorCommandBus();
        Root.Add(new EditorCommandSystem(
            Store,
            _commands,
            new EditorHistory(Store)));

        _editor = Store.CreateEntity(
            new LevelInfo("Untitled"),
            new LevelFile(""),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false),
            new EditorMetronome(false),
            new NoteSelection(default),
            LevelGroups.Create());
    }

    private Entity CreateSelected<T>(T item) where T : struct, IComponent
    {
        Entity entity = EditorItems.Create(Store, item);
        _editor.GetComponent<NoteSelection>().Note = entity;
        return entity;
    }

    [Test]
    public void RotatingTurnsASpikeInPlaceAsOneUndoStep()
    {
        _editor.AddComponent(new EditorPlacement());
        Entity spike = CreateSelected(new Spike(new Vector2(100, 100), new Vector2(50, 50)));

        _commands.Enqueue(EditorCommand.RotateSelection());
        Frame();

        Assert.That(spike.GetComponent<Spike>().RotationDegrees, Is.EqualTo(90f));
        Assert.That(spike.GetComponent<Spike>().Position, Is.EqualTo(new Vector2(100, 100)));
        Assert.That(_editor.GetComponent<EditorPlacement>().Degrees, Is.EqualTo(90f));

        _commands.Enqueue(EditorCommand.Undo());
        Frame();

        Assert.That(
            Store.Query<Spike>().ToEntityList()[0].GetComponent<Spike>().RotationDegrees,
            Is.Zero);
    }

    [Test]
    public void OneWayCyclesThroughEverySideAndUndoesInOneStep()
    {
        Entity block = CreateSelected(new Block(new Vector2(100, 100), new Vector2(50, 50)));

        foreach (BlockOneWay expected in new[]
                 {
                     BlockOneWay.Up, BlockOneWay.Right, BlockOneWay.Down, BlockOneWay.Left, BlockOneWay.Off,
                 })
        {
            _commands.Enqueue(EditorCommand.CycleOneWay());
            Frame();
            Assert.That(block.GetComponent<Block>().OneWay, Is.EqualTo(expected));
        }

        _commands.Enqueue(EditorCommand.CycleOneWay());
        Frame();
        _commands.Enqueue(EditorCommand.Undo());
        Frame();

        Assert.That(
            Store.Query<Block>().ToEntityList()[0].GetComponent<Block>().OneWay,
            Is.EqualTo(BlockOneWay.Off));
    }

    [Test]
    public void RotatingAndFlippingTurnAOneWaySide()
    {
        Entity block = CreateSelected(new Block(
            new Vector2(100, 100),
            new Vector2(50, 50),
            oneWay: BlockOneWay.Up));

        _commands.Enqueue(EditorCommand.RotateSelection());
        Frame();
        Assert.That(block.GetComponent<Block>().OneWay, Is.EqualTo(BlockOneWay.Right));

        _commands.Enqueue(EditorCommand.FlipSelection(horizontal: true));
        Frame();
        Assert.That(block.GetComponent<Block>().OneWay, Is.EqualTo(BlockOneWay.Left));

        _commands.Enqueue(EditorCommand.FlipSelection(horizontal: false));
        Frame();
        Assert.That(block.GetComponent<Block>().OneWay, Is.EqualTo(BlockOneWay.Left));
    }

    [Test]
    public void RotatingTurnsTheBrushWhenPlacingWithNoSelection()
    {
        _editor.AddComponent(new EditorPlacement { Mode = EditorPlacementMode.Spikes });

        _commands.Enqueue(EditorCommand.RotateSelection());
        Frame();

        Assert.That(_editor.GetComponent<EditorPlacement>().Degrees, Is.EqualTo(90f));
    }

    [Test]
    public void FlippingMirrorsTheSelectionAcrossItsCentre()
    {
        Entity left = EditorItems.Create(Store, new Note(
            0,
            new Vector2(200, 300),
            kind: NoteKind.Dash));
        Entity right = EditorItems.Create(Store, new Spike(
            new Vector2(350, 300),
            new Vector2(50, 50),
            90f));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { left, right });

        _commands.Enqueue(EditorCommand.FlipSelection(horizontal: true));
        Frame();

        Assert.That(left.GetComponent<Note>().Position.X, Is.EqualTo(400));
        Assert.That(right.GetComponent<Spike>().Position.X, Is.EqualTo(200));
        Assert.That(right.GetComponent<Spike>().RotationDegrees, Is.EqualTo(270f));
    }

    [TestCase(0f, true, 0f, TestName = "An upward spike keeps pointing up in a left-right flip")]
    [TestCase(0f, false, 180f, TestName = "An upward spike points down in an up-down flip")]
    [TestCase(90f, true, 270f, TestName = "A rightward spike points left in a left-right flip")]
    [TestCase(90f, false, 90f, TestName = "A rightward spike keeps pointing right in an up-down flip")]
    [TestCase(30f, true, 330f, TestName = "A slanted spike mirrors in a left-right flip")]
    [TestCase(30f, false, 150f, TestName = "A slanted spike mirrors in an up-down flip")]
    public void FlippingASpikeTurnsItTheRightWay(float degrees, bool horizontal, float expected)
    {
        Assert.That(SelectionRotation.FlippedSpikeDegrees(degrees, horizontal), Is.EqualTo(expected));
    }

    [Test]
    public void RotatingTurnsTheBrushForTheBlockToolToo()
    {
        _editor.AddComponent(new EditorPlacement { Mode = EditorPlacementMode.Blocks });

        _commands.Enqueue(EditorCommand.RotateSelection());
        Frame();

        Assert.That(_editor.GetComponent<EditorPlacement>().Degrees, Is.EqualTo(90f));
    }

    /// <summary>A 2x2 cluster of blocks, selected: one cell along, its copy overlaps the originals.</summary>
    private List<Entity> SelectBlockCluster()
    {
        var blocks = new List<Entity>();
        foreach (var corner in new[] { (100, 100), (150, 100), (100, 150), (150, 150) })
        {
            blocks.Add(EditorItems.Create(Store, new Block(
                new Vector2(corner.Item1, corner.Item2),
                new Vector2(50, 50))));
        }

        _editor.GetComponent<NoteSelection>().SetMany(blocks);
        return blocks;
    }

    [Test]
    public void PastingSeveralBlocksCopiesThemAsTheyAre()
    {
        SelectBlockCluster();

        _commands.Enqueue(EditorCommand.CopySelection());
        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();

        Assert.That(Store.Query<Block>().Count, Is.EqualTo(8));
        Assert.That(
            _editor.GetComponent<NoteSelection>().AllNotes.Select(item => item.GetComponent<Block>().Position),
            Is.EquivalentTo(new[]
            {
                new Vector2(150, 150), new Vector2(200, 150),
                new Vector2(150, 200), new Vector2(200, 200),
            }));
    }

    [Test]
    public void WithCollisionOnAPasteSkipsBlocksThatWouldOverlap()
    {
        PlacementCollision.Attach(Store.CreateEntity(), on: true);
        SelectBlockCluster();

        _commands.Enqueue(EditorCommand.CopySelection());
        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();

        Assert.That(Store.Query<Block>().Count, Is.EqualTo(7));
    }

    [Test]
    public void CopyAndPasteCarriesBlocksAndSpikesOneCellAlong()
    {
        Entity block = EditorItems.Create(Store, new Block(
            new Vector2(100, 100),
            new Vector2(100, 50)));
        Entity spike = EditorItems.Create(Store, new Spike(
            new Vector2(300, 300),
            new Vector2(50, 50),
            180f));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { block, spike });

        _commands.Enqueue(EditorCommand.CopySelection());
        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();

        Assert.That(Store.Query<Block>().Count, Is.EqualTo(2));
        Assert.That(Store.Query<Spike>().Count, Is.EqualTo(2));
        var selected = _editor.GetComponent<NoteSelection>().AllNotes;
        Entity pastedBlock = selected.First(item => item.HasComponent<Block>());
        Entity pastedSpike = selected.First(item => item.HasComponent<Spike>());
        Assert.That(pastedBlock.GetComponent<Block>().Position, Is.EqualTo(new Vector2(150, 150)));
        Assert.That(pastedSpike.GetComponent<Spike>().Position, Is.EqualTo(new Vector2(350, 350)));
        Assert.That(pastedSpike.GetComponent<Spike>().RotationDegrees, Is.EqualTo(180f));

        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();

        Assert.That(
            Store.Query<Spike>().ToEntityList().Select(item => item.GetComponent<Spike>().Position),
            Does.Contain(new Vector2(400, 400)),
            "pasting again steps further instead of stacking");

        _commands.Enqueue(EditorCommand.Undo());
        _commands.Enqueue(EditorCommand.Undo());
        Frame();

        Assert.That(Store.Query<Spike>().Count, Is.EqualTo(1));
    }

    [Test]
    public void PastingLandsOnTheSnapNearestThePlayheadAndKeepsTheSpacing()
    {
        Entity first = EditorItems.Create(Store, new Note(5_000, new Vector2(100, 100)));
        Entity second = EditorItems.Create(Store, new Note(5_250, new Vector2(200, 100)));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { first, second });
        ref Playhead playhead = ref _editor.GetComponent<Playhead>();
        playhead.DurationMs = 20_000;
        playhead.TimeMs = 8_130;
        long expected = EditorTiming.Snap(8_130, playhead);
        Assert.That(expected, Is.Not.EqualTo(8_130), "the playhead sits off the grid");

        _commands.Enqueue(EditorCommand.CopySelection());
        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();

        var pasted = _editor.GetComponent<NoteSelection>().AllNotes
            .Select(item => item.GetComponent<Note>().TimeMs)
            .OrderBy(time => time)
            .ToArray();
        Assert.That(pasted, Is.EqualTo(new[] { expected, expected + 250 }));
    }

    [Test]
    public void PastingGeometryWithNotesLeavesTheNoteTimingAlone()
    {
        Entity note = EditorItems.Create(Store, new Note(5_000, new Vector2(100, 100)));
        Entity block = EditorItems.Create(Store, new Block(new Vector2(100, 100), new Vector2(50, 50)));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { note, block });
        _editor.GetComponent<Playhead>().TimeMs = 8_000;
        _editor.GetComponent<Playhead>().DurationMs = 10_000;

        _commands.Enqueue(EditorCommand.CopySelection());
        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();

        Assert.That(
            Store.Query<Note>().ToEntityList().Select(item => item.GetComponent<Note>().TimeMs),
            Does.Contain(8_000L));
    }

    [Test]
    public void AppliesMetadataAndClampsTheSnap()
    {
        _commands.Enqueue(EditorCommand.SetName("Level"));
        _commands.Enqueue(EditorCommand.SetArtist("Artist"));
        _commands.Enqueue(EditorCommand.SetDifficulty("Hard"));
        _commands.Enqueue(EditorCommand.SetSnap(7));

        Frame();

        LevelInfo level = _editor.GetComponent<LevelInfo>();
        Playhead playhead = _editor.GetComponent<Playhead>();
        Assert.That(level.Name, Is.EqualTo("Level"));
        Assert.That(level.Artist, Is.EqualTo("Artist"));
        Assert.That(level.Difficulty, Is.EqualTo("Hard"));
        Assert.That(playhead.SnapDivision, Is.EqualTo(4));
    }

    [Test]
    public void TogglePlaybackAtEndSeeksToStart()
    {
        ref Playhead playhead = ref _editor.GetComponent<Playhead>();
        playhead.TimeMs = 1_000;
        playhead.DurationMs = 1_000;

        _commands.Enqueue(EditorCommand.TogglePlayback());
        Frame();

        Assert.That(playhead.Playing, Is.True);
        Assert.That(playhead.SeekRequestMs, Is.EqualTo(0));
    }

    [Test]
    public void DeleteSelectionIsUndoable()
    {
        Entity note = Store.CreateEntity(new Note(500, new Vector2(10, 20)));
        _editor.GetComponent<NoteSelection>().Note = note;

        _commands.Enqueue(EditorCommand.DeleteSelection());
        Frame();

        Assert.That(Store.Query<Note>().Count, Is.EqualTo(0));

        _commands.Enqueue(EditorCommand.Undo());
        Frame();

        Assert.That(Store.Query<Note>().Count, Is.EqualTo(1));
    }

    [Test]
    public void DeleteSelectionRemovesEverySelectedNote()
    {
        Entity first = Store.CreateEntity(new Note(500, new Vector2(10, 20)));
        Entity second = Store.CreateEntity(new Note(750, new Vector2(30, 40)));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { first, second });

        _commands.Enqueue(EditorCommand.DeleteSelection());
        Frame();

        Assert.That(Store.Query<Note>().Count, Is.Zero);
        Assert.That(_editor.GetComponent<NoteSelection>().Notes, Is.Empty);
    }

    [Test]
    public void DeleteSelectionRemovesMinesAndStartPoints()
    {
        Entity mine = Store.CreateEntity(new Mine(500, new Vector2(10, 20)));
        Entity start = Store.CreateEntity(new StartPoint(
            "Start", 500, new Vector2(30, 40),
            System.Numerics.Vector2.Zero, new Vector2(0, -1)));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { mine, start });

        _commands.Enqueue(EditorCommand.DeleteSelection());
        Frame();

        Assert.That(Store.Query<Mine>().Count, Is.Zero);
        Assert.That(Store.Query<StartPoint>().Count, Is.Zero);
        Assert.That(_editor.GetComponent<NoteSelection>().Notes, Is.Empty);
    }

    [Test]
    public void DeleteSelectionRemovesBlocksAndUndoRestoresThem()
    {
        Entity block = Store.CreateEntity(
            new Block(new Vector2(100, 100), new Vector2(200, 100)));
        _editor.GetComponent<NoteSelection>().Note = block;

        _commands.Enqueue(EditorCommand.DeleteSelection());
        Frame();

        Assert.That(Store.Query<Block>().Count, Is.Zero);

        _commands.Enqueue(EditorCommand.Undo());
        Frame();

        Assert.That(Store.Query<Block>().Count, Is.EqualTo(1));
        Assert.That(
            Store.Query<Block>().ToEntityList()[0].GetComponent<Block>().Position,
            Is.EqualTo(new Vector2(100, 100)));
    }

    [Test]
    public void PasteIsOneUndoableStep()
    {
        ref Playhead playhead = ref _editor.GetComponent<Playhead>();
        playhead.TimeMs = 1_000;
        playhead.DurationMs = 10_000;
        Entity note = Store.CreateEntity(new Note(100, new Vector2(10, 20)));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { note });

        _commands.Enqueue(EditorCommand.CopySelection());
        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();
        Assert.That(Store.Query<Note>().Count, Is.EqualTo(2));

        _commands.Enqueue(EditorCommand.Undo());
        Frame();

        Assert.That(Store.Query<Note>().Count, Is.EqualTo(1));
        Assert.That(
            Store.Query<Note>().ToEntityList()[0].GetComponent<Note>().TimeMs,
            Is.EqualTo(100));
    }

    [Test]
    public void MoveSelectionNudgesEveryEditorItem()
    {
        ref Playhead playhead = ref _editor.GetComponent<Playhead>();
        playhead.DurationMs = 10_000;
        Entity mine = Store.CreateEntity(new Mine(1_000, new Vector2(10, 20)));
        Entity start = Store.CreateEntity(new StartPoint(
            "Start", 2_000, new Vector2(30, 40),
            System.Numerics.Vector2.Zero, new Vector2(0, -1)));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { mine, start });

        _commands.Enqueue(EditorCommand.MoveSelection(1));
        Frame();

        // One 1/4 step at 120 BPM: 125 ms.
        Assert.That(mine.GetComponent<Mine>().TimeMs, Is.EqualTo(1_125));
        Assert.That(start.GetComponent<StartPoint>().TimeMs, Is.EqualTo(2_125));
    }

    [Test]
    public void CopyPastePreservesRelativeTimingAndPosition()
    {
        ref Playhead playhead = ref _editor.GetComponent<Playhead>();
        playhead.TimeMs = 1_000;
        playhead.DurationMs = 10_000;

        Entity first = Store.CreateEntity(
            new Note(100, new Vector2(10, 20)));
        Entity second = Store.CreateEntity(
            new Note(300, new Vector2(30, 40)));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { first, second });

        _commands.Enqueue(EditorCommand.CopySelection());
        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();

        Note[] notes = Store.Query<Note>().ToEntityList()
            .Select(entity => entity.GetComponent<Note>())
            .OrderBy(note => note.TimeMs)
            .ToArray();
        Assert.That(notes, Has.Length.EqualTo(4));
        Assert.That(notes.Select(note => note.TimeMs),
            Is.EqualTo(new long[] { 100, 300, 1_000, 1_200 }));
        Assert.That(notes[2].Position, Is.EqualTo(new Vector2(10, 20)));
        Assert.That(notes[3].Position, Is.EqualTo(new Vector2(30, 40)));
        Assert.That(_editor.GetComponent<NoteSelection>().AllNotes.Count,
            Is.EqualTo(2));
    }

    [Test]
    public void CopyPasteHandlesMinesAndStartPoints()
    {
        ref Playhead playhead = ref _editor.GetComponent<Playhead>();
        playhead.TimeMs = 2_000;
        playhead.DurationMs = 10_000;
        Entity mine = Store.CreateEntity(new Mine(100, new Vector2(10, 20)));
        mine.AddComponent(new ItemGroups(ItemGroups.Bit(3)));
        Entity start = Store.CreateEntity(new StartPoint(
            "Start", 400, new Vector2(30, 40),
            System.Numerics.Vector2.Zero, new Vector2(0, -1)));
        start.AddComponent(new ItemGroups(ItemGroups.Bit(5)));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { mine, start });

        _commands.Enqueue(EditorCommand.CopySelection());
        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();

        Assert.That(Store.Query<Mine>().Count, Is.EqualTo(2));
        Assert.That(Store.Query<StartPoint>().Count, Is.EqualTo(2));
        Assert.That(
            Store.Query<Mine>().ToEntityList().All(entity =>
                entity.GetComponent<ItemGroups>().Has(3)),
            Is.True);
        Assert.That(
            Store.Query<StartPoint>().ToEntityList().All(entity =>
                entity.GetComponent<ItemGroups>().Has(5)),
            Is.True);
        Assert.That(
            _editor.GetComponent<NoteSelection>().AllNotes.Count,
            Is.EqualTo(2));
    }

    [Test]
    public void SetTargetLeavesPlacedNotesAlone()
    {
        _editor.AddComponent(new EditorPlacement { Note = KeybindAction.Action2 });
        Entity note = Store.CreateEntity(
            new Note(500, new Vector2(10, 20), target: KeybindAction.Action2));
        _editor.GetComponent<NoteSelection>().Note = note;

        _commands.Enqueue(EditorCommand.SetTarget(KeybindAction.Action3));
        Frame();

        Assert.That(
            _editor.GetComponent<EditorPlacement>().Note,
            Is.EqualTo(KeybindAction.Action3));
        Assert.That(
            Store.Query<Note>().ToEntityList()[0].GetComponent<Note>().Target,
            Is.EqualTo(KeybindAction.Action2));
    }

    [Test]
    public void SetPlacementModeSwitchesTheEditorMode()
    {
        _editor.AddComponent(
            new EditorPlacement { Mode = EditorPlacementMode.Notes });

        _commands.Enqueue(
            EditorCommand.SetPlacementMode(EditorPlacementMode.Dash));
        Frame();

        Assert.That(
            _editor.GetComponent<EditorPlacement>().Mode,
            Is.EqualTo(EditorPlacementMode.Dash));
    }

    [Test]
    public void ZoomsTheEditorTimeline()
    {
        _editor.AddComponent(new TimelineView(1f));

        _commands.Enqueue(EditorCommand.ZoomTimeline(1));
        Frame();
        Assert.That(_editor.GetComponent<TimelineView>().Zoom, Is.EqualTo(2f));

        _commands.Enqueue(EditorCommand.ZoomTimeline(1));
        Frame();
        Assert.That(_editor.GetComponent<TimelineView>().Zoom, Is.EqualTo(4f));

        _commands.Enqueue(EditorCommand.ZoomTimeline(-1));
        Frame();
        Assert.That(_editor.GetComponent<TimelineView>().Zoom, Is.EqualTo(2f));
    }

    [Test]
    public void MovesSelectedNoteByOneSnapStepAndUndoes()
    {
        Entity note = Store.CreateEntity(new Note(1_000, new Vector2(10, 20)));
        _editor.GetComponent<NoteSelection>().Note = note;

        _commands.Enqueue(EditorCommand.MoveSelection(1));
        Frame();

        // One 1/4 step at 120 BPM: 125 ms.
        Assert.That(note.GetComponent<Note>().TimeMs, Is.EqualTo(1_125));

        _commands.Enqueue(EditorCommand.MoveSelection(-1));
        Frame();

        Assert.That(note.GetComponent<Note>().TimeMs, Is.EqualTo(1_000));

        _commands.Enqueue(EditorCommand.MoveSelection(1));
        Frame();
        _commands.Enqueue(EditorCommand.Undo());
        Frame();

        Assert.That(Store.Query<Note>().ToEntityList()[0].GetComponent<Note>().TimeMs,
            Is.EqualTo(1_000));
    }

    [Test]
    public void LoadingAFileTheLibraryCannotTakeLeavesTheLevelAlone()
    {
        // Songs only come from the library: a file it cannot import is not played raw.
        _commands.Enqueue(EditorCommand.LoadSong("/tmp/no-such-song-" + Guid.NewGuid().ToString("N") + ".mp3"));
        Frame();

        LevelInfo level = _editor.GetComponent<LevelInfo>();
        Assert.That(level.Song, Is.Null);
        Assert.That(level.SongId, Is.Empty);
        Assert.That(level.Name, Is.EqualTo("Untitled"));
        Assert.That(_editor.GetComponent<Playhead>().AudioRevision, Is.Zero);
    }

    [Test]
    public void ApplyLevelReplacesChart()
    {
        var document = new LevelDocument
        {
            Name = "Loaded",
            PlayfieldWidth = 1000,
            PlayfieldHeight = 1000,
        };
        document.Notes.Add(new NoteDocument
        {
            TimeMs = 750,
            X = 100,
            Y = 200,
        });

        _commands.Enqueue(EditorCommand.ApplyLevel(document, "/tmp/loaded.xmf"));
        Frame();

        Assert.That(_editor.GetComponent<LevelInfo>().Name, Is.EqualTo("Loaded"));
        Assert.That(_editor.GetComponent<Playhead>().Tempo.Points[0].Bpm, Is.EqualTo(120));
        Assert.That(_editor.GetComponent<LevelFile>().Path, Is.EqualTo("/tmp/loaded.xmf"));
        Assert.That(Store.Query<Note>().Count, Is.EqualTo(1));
    }
}
