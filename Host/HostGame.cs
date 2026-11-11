using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Input;
using Xanadu.Ecs;
using Xanadu.Host.Input;
using Xanadu.Host.Rendering;
using Xanadu.Levels;
using Xanadu.Storage;
using Xanadu.Systems;
using Xanadu.Systems.Canvas;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Host;

/// <summary>
/// The game on the MoonWorks host: builds the game's ECS world on
/// <see cref="HostApp"/>'s canvas, keyboard and pointer, plus what sits around
/// it: scene changes, the add-song dialog, the editor overlay (docs/host.md §5.8).
/// </summary>
internal sealed class HostGame : HostApp
{
    private readonly HostOptions _options;
    private readonly HostFileDialog _fileDialog;
    private readonly XanaduSystemRoot _systems;
    private Xanadu.Editor.EditorOverlay? _editorOverlay;

    public HostGame(HostOptions options) : base("Xanadu",
        new HostWindowOptions(options.WindowWidth, options.WindowHeight, options.ShotPath, options.ShotFrame))
    {
        _options = options;

        var director = new SceneDirector();
        director.Changed += OnSceneChanged;
        _systems = new XanaduSystemRoot(
            Entities,
            Canvas.RootRid,
            Pointer,
            director,
            UiStage.Design1080,
            keyboard: Keyboard,
            wheel: Pointer,
            passSurfaces: () => new HostPassSurfaces(Canvas, Renderer));
        _fileDialog = new HostFileDialog(MainWindow);
        _systems.Scenes.AddSongRequested = () => _fileDialog.OpenAudio(AddSong);
        Start();
    }

    protected override Friflo.Engine.ECS.Systems.SystemRoot Systems => _systems;

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

        _editorOverlay = new Xanadu.Editor.EditorOverlay(
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
                fromPlayhead: true))
        {
            PickShaderFile = then => _fileDialog.OpenShader(then),
            FileDialogOpen = () => _fileDialog.IsOpen,
        };
        _systems.Scenes.SaveEditor = () => Xanadu.Editor.EditorOverlay.SaveForLeaving(_editorOverlay);
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
                Console.WriteLine($"Playing {level.Document.Name} ({level.Path})");
                _systems.Scenes.Start(SceneKind.Profiles);
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

    protected override void Tick(float delta, float time)
    {
        _fileDialog.Poll();
        Keyboard.FeedPresses(Inputs.Keyboard, _systems.KeyPresses, key =>
            key != Xanadu.Systems.Input.DebugKeys.Screenshot && (!TextInput.Active || key == Key.Escape));
        _systems.Update(new UpdateTick(delta, time));
        _editorOverlay?.Tick();
        WarmLevelShaders();
    }

    protected override string ScreenshotName()
    {
        string folder = GamePaths.Globalize("user://screenshots");
        return Xanadu.Misc.ScreenshotNaming.Create("Xanadu", DateTime.Now, candidate => File.Exists(Path.Combine(folder, candidate)));
    }

    protected override void Notify(string message) => _systems.Toasts.Show(message);

    private readonly HashSet<ShaderSlot?[]> _warmedSlots = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Compiles a level's shader slots the frame its shaders appear (a level
    /// loaded for play or editing), rather than when each is first drawn.
    /// </summary>
    private void WarmLevelShaders()
    {
        foreach (Entity entity in Entities.Query<LevelShaders>().ToEntityList())
        {
            LevelShaders shaders = entity.GetComponent<LevelShaders>();
            if (shaders.Slots is null || !_warmedSlots.Add(shaders.Slots))
                continue;

            for (int slot = 1; slot <= ShaderLimits.Count; slot++)
                if (Xanadu.Systems.Shaders.LevelSlotMaterials.MaterialOf(shaders, slot) is { } material)
                    Renderer.Pipelines.Warm(material);
        }
    }

    protected override void Shutdown()
    {
        _editorOverlay?.Close();
        _systems.Dispose();
        _fileDialog.Dispose();
    }
}
