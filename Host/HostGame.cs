using System;
using System.IO;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Input;
using Xanadu.Ecs;
using Xanadu.Host.Input;
using Xanadu.Host.Rendering;
using Xanadu.Host.Text;
using Xanadu.Levels;
using Xanadu.Storage;
using Xanadu.Systems;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using MwColor = MoonWorks.Graphics.Color;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace Xanadu.Host;

/// <summary>
/// The MoonWorks host: owns the window and the GPU, builds the game's ECS world
/// on the host's canvas, keyboard and pointer, and runs the frame
/// (docs/moonworks-port.md step 2).
/// </summary>
/// <remarks>
/// Everything draws into <see cref="_target"/> at the game's 1920×1080 design
/// size, which is then blitted to the window: swapchain textures can't be read
/// back, and screenshots need to.
/// </remarks>
internal sealed class HostGame : Game
{
    private const uint Width = 1920, Height = 1080;
    private const TextureFormat TargetFormat = TextureFormat.R8G8B8A8Unorm;

    private readonly HostOptions _options;
    private readonly Texture _target;
    private readonly CanvasTree _canvas = new();
    private readonly CanvasRenderer _renderer;
    private readonly HostText _text;
    private readonly HostKeyboard _keyboard;
    private readonly HostPointer _pointer;
    private readonly HostTextInput _textInput;
    private readonly HostFileDialog _fileDialog;
    private readonly EntityStore _entities = new();
    private readonly XanaduSystemRoot _systems;
    private readonly DateTime _started = DateTime.UtcNow;
    private readonly HostFps _fps = new();
    private int _frames;
    private DateTime _lastUpdate = DateTime.UtcNow;
    private bool _screenshotRequested;

    public HostGame(HostOptions options) : base(
        new AppInfo("Xanadu", "Xanadu"),
        new WindowCreateInfo("Xanadu", options.WindowWidth, options.WindowHeight, ScreenMode.Windowed),
        FramePacingSettings.CreateCapped(60, 1),
        ShaderFormat.SPIRV,
        debugMode: false)
    {
        _options = options;
        _target = Texture.Create2D(GraphicsDevice, "frame", Width, Height, TargetFormat,
            TextureUsageFlags.ColorTarget | TextureUsageFlags.Sampler);
        _renderer = new CanvasRenderer(GraphicsDevice, _canvas, TargetFormat, Width, Height);

        GamePaths.UseRoots(FindGameFolder(), GamePaths.GodotUserFolder("Xanadu"));
        _text = new HostText(GraphicsDevice, texture =>
        {
            ulong id = _canvas.MintRid().Id;
            _renderer.Textures[id] = texture;
            return id;
        });
        SongStore.Shared = new SongStore(catalogPath: GamePaths.Globalize(SongStore.CatalogFile));

        _keyboard = new HostKeyboard(Inputs.Keyboard);
        _pointer = new HostPointer(Inputs.Mouse, _keyboard)
        {
            Scale = new Godot.Vector2((float)Width / options.WindowWidth, (float)Height / options.WindowHeight),
        };
        HostPlatform.Use(_canvas, () => new HostRectCanvas(_text), _keyboard, new CanvasServerRidFactory(), _fps);
        HostPlatform.ApplyVSync = ApplyVSync;
        ApplyVSync(true);

        var director = new SceneDirector();
        director.Changed += OnSceneChanged;
        _systems = new XanaduSystemRoot(
            _entities,
            _canvas.RootRid,
            _pointer,
            director,
            UiStage.Design1080,
            keyboard: _keyboard,
            wheel: _pointer);
        _textInput = new HostTextInput(_entities, MainWindow);
        _fileDialog = new HostFileDialog(MainWindow);
        _systems.Scenes.AddSongRequested = () => _fileDialog.OpenAudio(AddSong);
        Start();
    }

    /// <summary>
    /// Vertical sync on: present on vblank, one update per display refresh.
    /// Off: mailbox (or immediate) presents and updates up to 1000 a second,
    /// as Godot ran uncapped without vsync.
    /// </summary>
    private void ApplyVSync(bool enabled)
    {
        PresentMode mode = enabled ? PresentMode.VSync
            : GraphicsDevice.SupportsPresentMode(MainWindow, PresentMode.Mailbox) ? PresentMode.Mailbox
            : PresentMode.Immediate;
        GraphicsDevice.SetSwapchainParameters(MainWindow, SwapchainComposition.SDR, mode);
        SetFramePacingSettings(FramePacingSettings.CreateCapped(enabled ? RefreshRate() : 1000, 1));
    }

    private unsafe int RefreshRate()
    {
        SDL3.SDL.SDL_DisplayMode* display = SDL3.SDL.SDL_GetCurrentDisplayMode(SDL3.SDL.SDL_GetDisplayForWindow(MainWindow.Handle));
        return display is null || display->refresh_rate < 1 ? 60 : (int)MathF.Round(display->refresh_rate);
    }

    /// <summary>What the Godot <c>Main</c> did on a scene change, minus the editor overlay (step 9).</summary>
    private void OnSceneChanged(SceneKind scene)
    {
        _systems.Commands.Clear();
        if (scene == SceneKind.Editor)
            _systems.EditorHistory.Clear();
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

    /// <summary>The folder <c>res://</c> means: the one holding <c>project.godot</c>, above the exe.</summary>
    private static string FindGameFolder()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            if (File.Exists(Path.Combine(folder.FullName, "project.godot")))
                return folder.FullName;
        throw new InvalidOperationException("Could not find the game folder (project.godot) above " + AppContext.BaseDirectory);
    }

    protected override void Step() { }

    protected override void Update(TimeSpan step)
    {
        // Real time since the last update, as Godot's _Process had: a slow frame
        // drops the catch-up updates (one per tick), so the fixed step would undercount.
        DateTime now = DateTime.UtcNow;
        float delta = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        _fileDialog.Poll();
        _textInput.Update(Inputs.Keyboard, delta);
        if (Inputs.Keyboard.IsPressed(KeyCode.F12))
            _screenshotRequested = true;
        _keyboard.FeedPresses(Inputs.Keyboard, _systems.KeyPresses, key =>
            key != Xanadu.Systems.Input.DebugKeys.Screenshot && (!_textInput.Active || key == Godot.Key.Escape));
        _pointer.Poll();
        float time = (float)(now - _started).TotalSeconds;
        _systems.Update(new UpdateTick(delta, time));
        _pointer.EndFrame();
    }

    protected override void Draw(double alpha)
    {
        _fps.Frame();
        CommandBuffer commands = GraphicsDevice.AcquireCommandBuffer();
        Texture? swapchain = commands.AcquireSwapchainTexture(MainWindow);
        if (swapchain is null)
        {
            GraphicsDevice.Submit(commands);
            return;
        }

        _text.Atlas.Flush();
        _renderer.Prepare(commands);

        // Palette comes from the game assemblies, and its Godot Color struct works without the engine.
        Godot.Color background = Palette.Background;
        RenderPass pass = commands.BeginRenderPass(
            new ColorTargetInfo(_target, new MwColor(background.R, background.G, background.B, 1f)));
        _renderer.Draw(commands, pass);
        commands.EndRenderPass(pass);

        commands.Blit(_target, swapchain, Filter.Linear);

        _frames++;
        if (_screenshotRequested)
        {
            _screenshotRequested = false;
            SaveScreenshot(commands);
            return;
        }

        if (_options.ShotPath is { } path && _frames == _options.ShotFrame)
        {
            Console.WriteLine($"Canvas: {_canvas.Count} items, {_renderer.LastBatchCount} draw calls");
            Screenshot.Save(GraphicsDevice, commands, _target, Width, Height, path);
            Quit();
            return;
        }

        GraphicsDevice.Submit(commands);
    }

    /// <summary>F12, as in the Godot build: a PNG in <c>user://screenshots</c> and a toast.</summary>
    private void SaveScreenshot(CommandBuffer commands)
    {
        try
        {
            string folder = GamePaths.Globalize("user://screenshots");
            Directory.CreateDirectory(folder);
            string file = Xanadu.Misc.ScreenshotNaming.Create(
                "Xanadu", DateTime.Now, candidate => File.Exists(Path.Combine(folder, candidate)));
            Screenshot.Save(GraphicsDevice, commands, _target, Width, Height, Path.Combine(folder, file));
            _systems.Toasts.Show($"Screenshot saved: {file}");
        }
        catch (Exception exception)
        {
            _systems.Toasts.Show($"Screenshot failed: {exception.Message}");
        }
    }

    protected override void Destroy()
    {
        _systems.Dispose();
        _fileDialog.Dispose();
        _text.Dispose();
        _renderer.Dispose();
    }
}

/// <summary>Frames per second over the last second, for the debug overlay.</summary>
internal sealed class HostFps : Xanadu.Systems.Debug.IFpsSource
{
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private int _count;
    private double _fps;

    public void Frame()
    {
        _count++;
        double seconds = _clock.Elapsed.TotalSeconds;
        if (seconds < 1)
            return;
        _fps = _count / seconds;
        _count = 0;
        _clock.Restart();
    }

    public double GetFps() => _fps;
}
