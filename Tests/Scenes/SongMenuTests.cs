using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Profiles;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Xanadu.Systems.Scenes;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Scenes;

/// <summary>The right-click song menu on song select: timing, delete and cancel.</summary>
public sealed class SongMenuTests
{
    private readonly FakePointerInput _pointer = new();
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private SceneSystem _scenes = null!;
    private List<LevelEntry> _catalog = null!;
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _root = new SystemRoot(_store);
        _directory = Path.Combine(
            Path.GetTempPath(),
            "writer-song-menu-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);

        _catalog =
        [
            CreateLevel("Menu Level", "res://song-a.mp3"),
        ];

        _scenes = new SceneSystem(
            _store,
            new SceneDirector(),
            new Vector2(1920, 1080),
            new FakeVisualRidFactory(),
            () => _catalog,
            new KeybindSettings(persist: false),
            transitions: false,
            profiles: new ProfileStore(persist: false),
            pointer: _pointer);

        _root.Add(_scenes);
        _scenes.Start(SceneKind.SongSelect);
        Frame();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public void RightClickOnTheCardOpensTheMenu()
    {
        RightClick(UiStage.Full.Center + new Vector2(0, -10));

        Assert.That(Menu().Visible, Is.True);
    }

    [Test]
    public void RightClickOutsideTheCardLeavesTheMenuClosed()
    {
        RightClick(new Vector2(40, 40));

        Assert.That(Menu().Visible, Is.False);
    }

    [Test]
    public void DeleteAsksFirstAndThenDeletesTheLevel()
    {
        string path = _catalog[0].Path;
        RightClick(UiStage.Full.Center + new Vector2(0, -10));

        SceneSongMenu menu = Menu();
        menu.Delete.AddTag<Pressed>();
        Frame();

        SceneDeleteLevel confirm = Confirm();
        Assert.That(confirm.Visible, Is.True);
        Assert.That(File.Exists(path), Is.True);

        confirm.Confirm.AddTag<Pressed>();
        Frame();

        Assert.That(File.Exists(path), Is.False);
        Assert.That(Confirm().Visible, Is.False);
    }

    [Test]
    public void CancelKeepsTheLevel()
    {
        string path = _catalog[0].Path;
        RightClick(UiStage.Full.Center + new Vector2(0, -10));
        Menu().Delete.AddTag<Pressed>();
        Frame();

        Confirm().Cancel.AddTag<Pressed>();
        Frame();

        Assert.That(File.Exists(path), Is.True);
        Assert.That(Confirm().Visible, Is.False);
    }

    private void RightClick(Vector2 at)
    {
        _pointer.Current = new PointerInput(at, false, SecondaryPressed: true);
        Frame();
        _pointer.Current = new PointerInput(at, false);
        Frame();
    }

    private SceneSongMenu Menu() =>
        _store.Query<SceneSongMenu>().ToEntityList()[0]
            .GetComponent<SceneSongMenu>();

    private SceneDeleteLevel Confirm() =>
        _store.Query<SceneDeleteLevel>().ToEntityList()[0]
            .GetComponent<SceneDeleteLevel>();

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private LevelEntry CreateLevel(string name, string audioPath)
    {
        string path = Path.Combine(
            _directory,
            name.Replace(' ', '-') + XmfBinary.Extension);
        var document = new LevelDocument { Name = name };
        document.Notes.Add(new NoteDocument { TimeMs = 500, X = 100, Y = 200 });
        LevelStore.Save(document, path);

        return new LevelEntry(
            path,
            LevelStore.Load(path),
            new SongDocument { Id = "song-a", Title = name, AudioPath = audioPath });
    }
}
