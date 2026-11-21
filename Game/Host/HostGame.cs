using Xanadu.Storage.Levels;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Input;
using Xanadu.Core.Maths;
using Xanadu.Core.Rendering.Rhythm;
using Xanadu.Core.Storage;
using Xanadu.Model.Ecs;
using Xanadu.Model.Ecs.Components;
using Xanadu.Host.Input;
using Xanadu.Host.Rendering;
using Xanadu.Model.Levels;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Xanadu.Wiring;
using Xanadu.Wiring.Scenes;

namespace Xanadu.Host;

/// <summary>
/// The game on the MoonWorks host: builds the game's ECS world on
/// <see cref="HostApp"/>'s canvas, keyboard and pointer, plus what sits around
/// it: scene changes, the add-song dialog, the editor overlay (Docs/host.md §5.8).
/// </summary>
internal sealed class HostGame : HostApp
{
    // The game's shader globals, declared before HostApp's constructor warms the shipped
    // effects; its files hooked in before anything loads particles or rig art.
    static HostGame()
    {
        RhythmGlobals.Declare();
        Xanadu.Storage.GameFiles.Install();
    }

    private readonly HostOptions _options;
    private readonly HostFileDialog _fileDialog;
    private readonly XanaduSystemRoot _systems;
    private Xanadu.Screens.Editor.EditorOverlay? _editorOverlay;

    public HostGame(HostOptions options) : base("Xanadu",
        new HostWindowOptions(options.WindowWidth, options.WindowHeight, options.ShotPath, options.ShotFrame))
    {
        _options = options;
        SongStore.Shared = new SongStore(catalogPath: GamePaths.Full(SongStore.CatalogFile));

        var director = new SceneDirector();
        director.Changed += OnSceneChanged;
        _systems = new XanaduSystemRoot(
            Entities,
            Canvas.RootHandle,
            Pointer,
            director,
            UiStage.Design1080,
            keyboard: Keyboard,
            wheel: Pointer,
            autoPlay: options.Auto);
        _fileDialog = new HostFileDialog(MainWindow);
        _systems.Scenes.AddSongRequested = () => _fileDialog.OpenAudio(AddSong);
        UseSystems(_systems);
        Start();
    }

    /// <summary>What the Godot <c>Main</c> did on a scene change: the editor gets its overlay while it is up.</summary>
    private void OnSceneChanged(SceneKind scene)
    {
        _systems.Commands.Clear();
        if (scene != SceneKind.Editor)
        {
            _editorOverlay?.Close();
            _editorOverlay = null;
            return;
        }

        _systems.EditorHistory.Clear();
        if (_editorOverlay is not null)
            return;

        _editorOverlay = new Xanadu.Screens.Editor.EditorOverlay(
            Entities,
            _systems.Commands,
            Pointer,
            () => _systems.Scenes.TryBack(),
            () => _systems.Scenes.TryForward(),
            (document, path, playheadMs) => _systems.Scenes.StartEditorPreview(
                LevelStore.Entry(path, document),
                playheadMs),
            onPreviewHere: (document, path, playheadMs) => _systems.Scenes.StartEditorPreview(
                LevelStore.Entry(path, document),
                playheadMs,
                fromPlayhead: true),
            history: _systems.EditorHistory)
        {
            FileDialogOpen = () => _fileDialog.IsOpen,
        };
        _systems.Scenes.SaveEditor = () => Xanadu.Screens.Editor.EditorOverlay.SaveForLeaving(_editorOverlay);
    }

    private void AddSong(string path)
    {
        try
        {
            LoadedSong song = SongStore.Shared.Import(path);
            _systems.Scenes.OpenSongTiming(song.Id, SceneKind.SongSelect);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Add song failed: {exception.Message}");
            _systems.Toasts.Show($"Add song failed: {exception.Message}");
        }
    }

    private void Start()
    {
        if (_options.Demo && DemoLevel.Create() is { } demo)
        {
            _systems.Scenes.Start(SceneKind.Profiles);
            if (_options.Editor)
                _systems.Scenes.StartEditor(demo);
            else
                _systems.Scenes.StartPlay(demo);
            return;
        }

        if (_options.Play is { } name)
        {
            LevelEntry? level = LevelStore.List()
                .Where(entry => !entry.IsBlocked)
                .FirstOrDefault(entry =>
                    entry.Document.Name.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(entry.Path).Contains(name, StringComparison.OrdinalIgnoreCase));
            if (level is not null)
            {
                Console.WriteLine($"{(_options.Editor ? "Editing" : "Playing")} {level.Document.Name} ({level.Path})");
                _systems.Scenes.Start(SceneKind.Profiles);
                if (_options.Editor)
                    _systems.Scenes.StartEditor(level);
                else
                    _systems.Scenes.StartPlay(level);
                return;
            }

            Console.WriteLine($"No playable level matches '{name}'; starting normally. Levels:");
            foreach (LevelEntry entry in LevelStore.List())
                Console.WriteLine($"  {Path.GetFileName(entry.Path)}: {entry.Document.Name}{(entry.IsBlocked ? " (song missing)" : "")}");
        }

        if (_options.Scene is { } scene)
        {
            _systems.Scenes.Start(Enum.Parse<SceneKind>(scene, ignoreCase: true));
            return;
        }

        _systems.Scenes.Start(_options.Editor ? SceneKind.Editor : SceneKind.Profiles);
    }

    protected override void BeforeSystems(float delta)
    {
        _fileDialog.Poll();
        Keyboard.FeedPresses(Inputs.Keyboard, _systems.KeyPresses, key =>
            key != DebugKeys.Screenshot && (!TextInput.Active || key == Key.Escape));
    }

    protected override void Tick(float delta, float time)
    {
        _editorOverlay?.Tick();
    }

    protected override string ScreenshotName()
    {
        string folder = GamePaths.Full("user://screenshots");
        return Xanadu.Wiring.ScreenshotNaming.Create("Xanadu", DateTime.Now, candidate => File.Exists(Path.Combine(folder, candidate)));
    }

    protected override void Notify(string message) => _systems.Toasts.Show(message);

    protected override void Shutdown()
    {
        _editorOverlay?.Close();
        _systems.Dispose();
        _fileDialog.Dispose();
    }
}
