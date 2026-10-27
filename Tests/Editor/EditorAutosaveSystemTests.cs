using System;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor.Recovery;
using Xanadu.Levels;
using Xanadu.Settings;
using Xanadu.Systems.Editor;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorAutosaveSystemTests
{
    private const string LevelPath = "/levels/mine.xmf";

    private string _directory = null!;
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private LevelRecoveryStore _recovery = null!;
    private Entity _editor;
    private DateTimeOffset _now = new(2026, 10, 27, 14, 0, 0, TimeSpan.Zero);

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "recovery-" + Path.GetRandomFileName());
        _store = new EntityStore();
        _recovery = new LevelRecoveryStore(_directory);
        _editor = NewEditor(LevelPath);
        _root = new SystemRoot(_store);
        _root.Add(new EditorAutosaveSystem(_store, _recovery, () => _now, intervalSeconds: 10));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public void AnUntouchedLevelIsNeverAutosaved()
    {
        Pass(seconds: 60);

        Assert.That(_recovery.Read(LevelPath), Is.Null);
    }

    [Test]
    public void EditsAreAutosavedOnTheInterval()
    {
        Pass(0.1f);
        AddNote(500);

        Pass(5);
        Assert.That(_recovery.Read(LevelPath), Is.Null, "not before the interval is up");

        Pass(6);
        LevelRecoveryStore.Entry? saved = _recovery.Read(LevelPath);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.Level.Notes, Has.Count.EqualTo(1));
        Assert.That(saved.SavedAt, Is.EqualTo(_now));
        Assert.That(saved.LevelPath, Is.EqualTo(LevelPath));
    }

    [Test]
    public void UndoingBackToCleanRemovesTheAutosave()
    {
        Pass(0.1f);
        Entity note = AddNote(500);
        Pass(11);
        Assert.That(_recovery.Read(LevelPath), Is.Not.Null);

        note.DeleteEntity();
        Pass(11);

        Assert.That(_recovery.Read(LevelPath), Is.Null);
    }

    [Test]
    public void SavingClearsTheAutosaveAndMakesTheSavedStateClean()
    {
        Pass(0.1f);
        AddNote(500);
        Pass(11);
        Assert.That(_recovery.Read(LevelPath), Is.Not.Null);

        _editor.AddTag<EditorSaved>();
        Pass(0.1f);
        Assert.That(_recovery.Read(LevelPath), Is.Null);

        Pass(30);
        Assert.That(_recovery.Read(LevelPath), Is.Null, "the saved state is clean, not unsaved");
    }

    [Test]
    public void SavingAnUntitledLevelClearsTheUntitledSlot()
    {
        Entity untitled = NewEditor("");
        _editor.DeleteEntity();
        _editor = untitled;
        Pass(0.1f);
        AddNote(250);
        Pass(11);
        Assert.That(_recovery.Read(""), Is.Not.Null);

        _editor.GetComponent<LevelFile>().Path = "/levels/new.xmf";
        _editor.AddTag<EditorSaved>();
        Pass(0.1f);

        Assert.That(_recovery.Read(""), Is.Null);
    }

    [Test]
    public void ReopeningALevelWithNewerAutosaveOffersIt()
    {
        LevelDocument edited = LevelDocumentMapper.Capture(_store, _editor);
        edited.Notes.Add(new NoteDocument { TimeMs = 900, X = 5, Y = 5 });
        _recovery.Write(LevelPath, edited, _now);

        Pass(0.1f);

        Assert.That(_editor.HasComponent<EditorRecoveryOffer>(), Is.True);
        Assert.That(_editor.GetComponent<EditorRecoveryOffer>().Level.Notes, Has.Count.EqualTo(1));
        Assert.That(_editor.GetComponent<EditorRecoveryOffer>().SavedAt, Is.EqualTo(_now));
    }

    [Test]
    public void AnAutosaveIdenticalToTheLoadedLevelIsDiscardedNotOffered()
    {
        _recovery.Write(LevelPath, LevelDocumentMapper.Capture(_store, _editor), _now);

        Pass(0.1f);

        Assert.That(_editor.HasComponent<EditorRecoveryOffer>(), Is.False);
        Assert.That(_recovery.Read(LevelPath), Is.Null);
    }

    [Test]
    public void WhileAnOfferIsPendingTheAutosaveIsNotOverwritten()
    {
        LevelDocument edited = LevelDocumentMapper.Capture(_store, _editor);
        edited.Notes.Add(new NoteDocument { TimeMs = 900, X = 5, Y = 5 });
        _recovery.Write(LevelPath, edited, _now);
        Pass(0.1f);

        AddNote(100);
        Pass(30);

        Assert.That(_recovery.Read(LevelPath)!.Level.Notes[0].TimeMs, Is.EqualTo(900));
    }

    [Test]
    public void NotesAtTheSameTimeDoNotLookLikeAnEdit()
    {
        AddNote(500, x: 10);
        AddNote(500, x: 20);
        Pass(0.1f);

        // The mapper's sort is by time only, so ties may come out in either order.
        Pass(60);

        Assert.That(_recovery.Read(LevelPath), Is.Null);
    }

    [Test]
    public void ALevelLoadedFromDiskBecomesTheNewCleanState()
    {
        Pass(0.1f);
        _editor.GetComponent<LevelFile>().Path = "/levels/other.xmf";
        AddNote(500);
        _editor.AddTag<EditorBaselineStale>();
        Pass(0.1f);

        Pass(60);

        Assert.That(_recovery.Read("/levels/other.xmf"), Is.Null);
    }

    private Entity NewEditor(string path)
    {
        Entity editor = _store.CreateEntity(
            new LevelInfo("Mine", ""),
            new LevelFile(path),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false),
            new EditorUiRequest(),
            LevelGroups.Create());
        return editor;
    }

    private Entity AddNote(long time, float x = 100) => _store.CreateEntity(
        new Note(time, new Vector2(x, 200), KeybindAction.M, NoteKind.Hit, DashDirection.Up, 0, CharacterKind.Contact));

    private void Pass(float seconds) =>
        _root.Update(new UpdateTick(seconds, 0));
}
