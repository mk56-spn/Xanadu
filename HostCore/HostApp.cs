using System.Collections.Generic;
using System.IO;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Input;
using SDL3;
using Xanadu.Core.Storage;
using Xanadu.Core.Theme;
using Xanadu.Host.Input;
using Xanadu.Host.Rendering;
using Xanadu.Host.Text;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Debug;
using Xanadu.Ui.Systems.Ui;
using Xanadu.Ui.Terminal;
using Color = Xanadu.Core.Maths.Color;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;
using MwColor = MoonWorks.Graphics.Color;

namespace Xanadu.Host;

/// <summary>Window size and the <c>--shot</c> settings every host exe takes.</summary>
internal sealed record HostWindowOptions(uint Width, uint Height, string? ShotPath, int ShotFrame);

/// <summary>How the host boots the ECS UI (<see cref="UiRoot"/>) on <see cref="HostApp.Entities"/>.</summary>
internal enum HostUi
{
    /// <summary>The UI root, with the F1, F3 and F4 overlays, stepping the exe's root (<see cref="HostApp.UseSystems"/>).</summary>
    Standard,

    /// <summary>The same, without hover tooltips (the animator draws its own).</summary>
    NoTooltips,

    /// <summary>No UI on the host's store: only the visual-test harness, which makes a store and a UI root per scene.</summary>
    None,
}

/// <summary>
/// What every MoonWorks exe shares (Docs/host.md): the window, the
/// 1920×1080 frame drawn by the host's canvas renderer and blitted to the
/// window, fonts, keyboard, pointer and text input, the shader library, vsync,
/// F12 screenshots and <c>--shot</c>. The host owns the UI (<see cref="UiRoot"/>):
/// every exe gets the same UI systems and the F1 frame rate, F3 ECS and F4 UI tree
/// overlays without wiring them. An exe owns its own root, on the same store, and
/// hands it over with <see cref="UseSystems"/>; the UI steps it after input and
/// before layout.
/// </summary>
/// <remarks>
/// Swapchain textures can't be read back, so everything draws into
/// <see cref="Target"/> first; screenshots read that.
/// </remarks>
internal abstract class HostApp : Game
{
    protected const uint Width = 1920, Height = 1080;
    private const TextureFormat TargetFormat = TextureFormat.R8G8B8A8Unorm;

    private readonly HostWindowOptions _window;
    private readonly DateTime _started = DateTime.UtcNow;
    private DateTime _lastUpdate = DateTime.UtcNow;
    private int _frames;
    private bool _screenshotRequested;
    private string? _requestedShot;
    private readonly HostFrameProbe? _probe;
    private readonly HostFramePhases _phases = new();
    private readonly Autopsy.AutopsyRecorder? _autopsy;
    private readonly Autopsy.CpuSampler? _sampler;

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "gettid")]
    private static extern int GetThreadId();
    private bool _drawnSinceUpdate = true;

    /// <summary>Shown while the host and the exe load; let go once the first real frame is drawn.</summary>
    private HostSplash? _splash;

    protected Texture Target { get; }
    protected CanvasTree Canvas { get; } = new();
    protected CanvasRenderer Renderer { get; }
    protected HostText Text { get; }
    protected HostKeyboard Keyboard { get; }
    protected HostPointer Pointer { get; }
    protected HostTextInput TextInput { get; }
    protected HostFps Fps { get; } = new();

    private readonly Rendering.VulkanMemoryBudget _gpuMemory = new();
    protected EntityStore Entities { get; } = new();

    /// <summary>The host's UI; null with <see cref="HostUi.None"/>.</summary>
    private readonly UiRoot? _ui;

    /// <summary>
    /// The text terminal's commands (Ctrl+`). The host adds its own (vsync,
    /// frame cap, window mode…); an exe adds whatever it wants to be told.
    /// </summary>
    protected TerminalCommands Terminal { get; } = new();

    /// <summary>True while the terminal is down: it has the keyboard, so exes hold their shortcuts back.</summary>
    protected bool TerminalOpen => _ui?.Terminal?.IsOpen ?? false;

    private bool _vsync = true;

    /// <summary>
    /// <c>XANADU_TERMINAL="vsync off;maxfps 144"</c>: lines run at launch, once the exe has
    /// added its commands, with the terminal left down to show what they said.
    /// </summary>
    private string? _startupLines;
    private int _maxFps;

    /// <summary>The canvas the host's UI draws with, for exe systems that draw alongside it.</summary>
    protected IRectCanvas UiCanvas => _ui!.Canvas;

    /// <summary>Hands the exe's own root to the UI's frame: run after UI input, before layout. The exe keeps owning it.</summary>
    protected void UseSystems(SystemRoot systems) => _ui!.Run(systems);

    /// <summary>Seconds since the window opened: the TIME effects see.</summary>
    protected float Seconds => (float)(DateTime.UtcNow - _started).TotalSeconds;

    /// <summary>Runs before any instance's base constructor makes the graphics device, which is when the layer must be in place.</summary>
    static HostApp()
    {
        GpuTimes.Install();
        HostLateTickLog.Install();
    }

    protected HostApp(string title, HostWindowOptions window, HostUi ui = HostUi.Standard) : base(
        new AppInfo("Xanadu", title),
        new WindowCreateInfo(title, window.Width, window.Height, ScreenMode.Windowed),
        FramePacingSettings.CreateCapped(60, 1),
        ShaderFormat.SPIRV,
        debugMode: false)
    {
        _window = window;
        Target = Texture.Create2D(GraphicsDevice, "frame", Width, Height, TargetFormat,
            TextureUsageFlags.ColorTarget | TextureUsageFlags.Sampler);
        UseGameRoots();
        HostWindowIcon.Apply(MainWindow.Handle);
        // The window is up from here: everything below loads behind the splash.
        _splash = new HostSplash(GraphicsDevice, MainWindow);
        _splash.Show(0f, force: true);
        SlangShaders.LibraryPath = GamePaths.Full("Assets/Shaders/Library");
        ShaderConstants.Use(SlangShaders.LoadConstants());
        ShaderChains.Use(SlangShaders.LoadChains());
        SlangShaders.CacheFolder = GamePaths.Full("user://shader_cache_slang");
        _splash.Show(0.1f);
        Renderer = new CanvasRenderer(GraphicsDevice, Canvas, TargetFormat, Width, Height);
        _splash.Show(0.3f);
        Text = new HostText(GraphicsDevice, texture =>
        {
            ulong id = Canvas.MintHandle().Id;
            Renderer.Textures[id] = texture;
            return id;
        });

        Keyboard = new HostKeyboard(Inputs.Keyboard);
        Pointer = new HostPointer(Inputs.Mouse, Keyboard)
        {
            Scale = new Vector2((float)Width / window.Width, (float)Height / window.Height),
        };
        HostPlatform.Use(Canvas, () => new HostRectCanvas(Text), Keyboard, new CanvasServerItemFactory(loadsAssets: true), Fps);
        HostPlatform.Particles = Renderer.Particles;
        HostPlatform.GpuMemory = _gpuMemory;
        HostPlatform.FramePhases = _phases;
        TextureServer.Use(new HostTextures(GraphicsDevice, Canvas, Renderer));
        UiIcons.Loader = UiIcons.FromResources;
        HostPlatform.GlyphStrip = atlas =>
        {
            Handle rid = Canvas.MintHandle();
            Renderer.Textures[rid.Id] = GlyphStrips.Build(GraphicsDevice, Text, atlas);
            return rid;
        };
        // The slow step on a cold shader cache: one shader at a time, the bar moving with each.
        Renderer.Pipelines.WarmShipped((done, total) => _splash.Show(0.35f + 0.5f * done / total));
        HostPlatform.DescribeMaterial = DescribeMaterial;
        HostPlatform.ApplyVSync = ApplyVSync;
        ApplyVSync(true);
        HostPlatform.ApplyWindowMode = ApplyWindowMode;
        TextInput = new HostTextInput(Entities, MainWindow);
        HostPlatform.TextEntry = TextInput;
        HostPlatform.MeasureMono = (text, size) => Text.Width(text, Xanadu.Host.Text.FontKind.Mono, size);
        _probe = HostFrameProbe.Enabled ? new HostFrameProbe() : null;

        if (ui != HostUi.None)
        {
            _ui = new UiRoot(
                Entities,
                Canvas.RootHandle,
                Pointer,
                new Vector2(Width, Height),
                Keyboard,
                Pointer,
                tooltips: ui != HostUi.NoTooltips,
                terminal: Terminal);
            HostTerminal.AddCommands(this);
        }

        _startupLines = Environment.GetEnvironmentVariable("XANADU_TERMINAL");
        _splash.Show(0.9f, force: true);

        // Every launch is recorded for the Autopsy tool and saved when the window closes.
        // Shot runs (visual tests) leave the last real launch alone unless XANADU_AUTOPSY=1.
        if (RecordsAutopsy && (window.ShotPath is null || Environment.GetEnvironmentVariable("XANADU_AUTOPSY") == "1"))
        {
            string app = AutopsyName(title);
            _autopsy = new Autopsy.AutopsyRecorder(app, 1000f / RefreshRate(), OperatingSystem.IsLinux() ? GetThreadId() : 0);
            GpuTimes.FrameTimed = _autopsy.AddGpu;
            _sampler = Autopsy.CpuSampler.Start(app);
            _autopsy.HasTrace = _sampler is not null;
            AppDomain.CurrentDomain.UnhandledException += (_, _) => SaveAutopsy();
        }
    }

    /// <summary>The systems the frame autopsy times, one by one: the UI's, with the exe's root under its step.</summary>
    protected virtual SystemRoot? Systems => _ui;

    /// <summary>False for an exe whose launches shouldn't be recorded (the Autopsy tool itself).</summary>
    protected virtual bool RecordsAutopsy => true;

    /// <summary>"Xanadu" is the game; "Xanadu Labs" is "labs".</summary>
    private static string AutopsyName(string title)
    {
        string name = title.StartsWith("Xanadu ", StringComparison.Ordinal) ? title["Xanadu ".Length..] : title == "Xanadu" ? "game" : title;
        return name.ToLowerInvariant().Replace(' ', '-');
    }

    private void SaveAutopsy()
    {
        if (_autopsy is null)
            return;
        try
        {
            Autopsy.AutopsyStore.Save(_autopsy.Snapshot());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Frame autopsy not saved: {exception.Message}");
        }
    }

    /// <summary>Saves the next frame to <paramref name="path"/> and quits, for an exe that times its own shot.</summary>
    protected void RequestShot(string path) => _requestedShot = path;

    /// <summary>
    /// The exe's frame, with real seconds since the last one; the UI, and the exe's
    /// root inside it, have just run.
    /// </summary>
    protected abstract void Tick(float delta, float time);

    /// <summary>Before the host's systems run: input the exe feeds them (the animator's key presses).</summary>
    protected virtual void BeforeSystems(float delta) { }

    /// <summary>Where an F12 shot goes, and its name; the game uses its own naming.</summary>
    protected virtual string ScreenshotName() => $"Xanadu {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png";

    /// <summary>Tells the user about a screenshot; the game shows a toast.</summary>
    protected virtual void Notify(string message) => Console.WriteLine(message);

    /// <summary>
    /// Records GPU work the canvas will sample this frame (an exe's own
    /// offscreen passes), before the canvas renders.
    /// </summary>
    protected virtual void RenderOffscreen(CommandBuffer commands) { }

    /// <summary>Called when the window closes, before the host's own resources go.</summary>
    protected virtual void Shutdown() { }

    /// <summary>
    /// Vertical sync on: present on vblank, one update per display refresh.
    /// Off: mailbox (or immediate) presents, one update per frame, as fast as
    /// the machine goes, as Godot ran uncapped without vsync.
    /// </summary>
    private void ApplyVSync(bool enabled)
    {
        _vsync = enabled;
        PresentMode mode = enabled ? PresentMode.VSync
            : GraphicsDevice.SupportsPresentMode(MainWindow, PresentMode.Mailbox) ? PresentMode.Mailbox
            : PresentMode.Immediate;
        GraphicsDevice.SetSwapchainParameters(MainWindow, SwapchainComposition.SDR, mode);
        // MoonWorks' Uncapped mode draws without updating between timesteps, so
        // "off" is a cap no frame reaches: every frame still gets one update.
        SetFramePacingSettings(FramePacingSettings.CreateCapped(enabled ? RefreshRate() : _maxFps > 0 ? _maxFps : 100_000, 1));
        // Under KDE fullscreen the compositor scans our buffer out and holds it
        // until the next flip; with two frames in flight vsync locked at half
        // the refresh rate with the GPU idle. A third costs at most one refresh
        // of latency while vsync is on.
        GraphicsDevice.SetAllowedFramesInFlight(3);
    }

    /// <summary>
    /// Windowed: a decorated window at the launch size. Fullscreen: SDL
    /// exclusive fullscreen at the display's current mode. Borderless: SDL
    /// desktop fullscreen, a borderless window covering the whole display.
    /// </summary>
    private unsafe void ApplyWindowMode(WindowMode mode)
    {
        IntPtr window = MainWindow.Handle;
        uint display = SDL.SDL_GetDisplayForWindow(window);
        switch (mode)
        {
            case WindowMode.Fullscreen:
                SDL.SDL_SetWindowBordered(window, true);
                SDL.SDL_DisplayMode* current = SDL.SDL_GetCurrentDisplayMode(display);
                if (current is not null)
                    SDL.SDL_SetWindowFullscreenMode(window, ref *current);
                SDL.SDL_SetWindowFullscreen(window, true);
                break;
            case WindowMode.Borderless:
                // Desktop fullscreen: no mode of its own, the window covers the
                // display at the desktop's resolution.
                SDL.SDL_SetWindowFullscreenMode(window, ref System.Runtime.CompilerServices.Unsafe.NullRef<SDL.SDL_DisplayMode>());
                SDL.SDL_SetWindowFullscreen(window, true);
                break;
            default:
                SDL.SDL_SetWindowFullscreen(window, false);
                SDL.SDL_SetWindowBordered(window, true);
                SDL.SDL_SetWindowSize(window, (int)_window.Width, (int)_window.Height);
                break;
        }
        SDL.SDL_SyncWindow(window);
    }

    /// <summary>What the terminal reads and sets; see <see cref="HostTerminal"/>.</summary>
    internal bool VSync
    {
        get => _vsync;
        set => ApplyVSync(value);
    }

    /// <summary>The frame cap while vsync is off; 0 for none.</summary>
    internal int MaxFps
    {
        get => _maxFps;
        set
        {
            _maxFps = Math.Max(0, value);
            ApplyVSync(_vsync);
        }
    }

    internal int DisplayHz => RefreshRate();

    internal double CurrentFps => Fps.GetFps();

    internal TerminalCommands Commands => Terminal;

    internal void SetWindowMode(WindowMode mode) => ApplyWindowMode(mode);

    internal void RequestScreenshot() => _screenshotRequested = true;

    private unsafe int RefreshRate()
    {
        SDL.SDL_DisplayMode* display = SDL.SDL_GetCurrentDisplayMode(SDL.SDL_GetDisplayForWindow(MainWindow.Handle));
        return display is null || display->refresh_rate < 1 ? 60 : (int)MathF.Round(display->refresh_rate);
    }

    /// <summary>Points the game folder and user:// where every host exe has them; for a command that runs without a window.</summary>
    internal static void UseGameRoots() => GamePaths.UseRoots(FindGameFolder(), GamePaths.UserFolder("xanadu"));

    /// <summary>The game folder: the one holding <see cref="GamePaths.RootMarker"/>, above the exe.</summary>
    private static string FindGameFolder()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            if (GamePaths.IsGameFolder(folder.FullName) && Directory.Exists(Path.Combine(folder.FullName, "Assets", "Shaders")))
                return folder.FullName;
        throw new InvalidOperationException($"Could not find the game folder ({GamePaths.RootMarker}) above " + AppContext.BaseDirectory);
    }

    /// <summary>A material's values as the compiled effect declares them, for the F2 tuner.</summary>
    private IReadOnlyList<MaterialParam>? DescribeMaterial(CanvasMaterial material)
    {
        CanvasEffect effect = Renderer.Pipelines.Effect(material);
        if (effect == Renderer.Pipelines.Plain)
            return null;

        return effect.Compiled.Params.Select(MaterialParams.Describe).ToList();
    }

    protected override void Step() { }

    protected override void Update(TimeSpan step)
    {
        // Real time since the last update, as Godot's _Process had: a slow frame
        // drops the catch-up updates (one per tick), so the fixed step would undercount.
        DateTime now = DateTime.UtcNow;
        float delta = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        if (_drawnSinceUpdate)
            _autopsy?.BeginFrame();
        _drawnSinceUpdate = false;
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        long lap = started;
        _probe?.BeginPhase();
        TextInput.Update(Inputs.Keyboard, delta);
        // While a code editor has the keys, F12 is its go-to-declaration.
        if (Inputs.Keyboard.IsPressed(KeyCode.F12) && !TextInput.Claimed)
            _screenshotRequested = true;
        FollowWindowSize();
        Pointer.Poll();
        float time = (float)(now - _started).TotalSeconds;
        Xanadu.Core.Tweens.ClipClocks.SetSeconds(time);
        BeforeSystems(delta);
        _phases.Lap(HostFramePhases.Phase.Input, ref lap);
        if (_startupLines is { } lines && _ui?.Terminal is { } terminal)
        {
            _startupLines = null;
            foreach (string line in lines.Split(';'))
                terminal.Run(line);
            terminal.Show(true);
        }
        _ui?.Update(new UpdateTick(delta, time));
        _phases.Lap(HostFramePhases.Phase.UiAndSystems, ref lap);
        Tick(delta, time);
        Pointer.EndFrame();
        _phases.Lap(HostFramePhases.Phase.ExeTick, ref lap);
        _phases.Add(HostFramePhases.Phase.Update, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        _probe?.EndUpdate();
        if (_autopsy is not null)
        {
            if (Systems is { } systems)
                _autopsy.SampleSystems(systems);
            _autopsy.AddUpdate(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    /// <summary>
    /// Keeps the pointer in design pixels when the window is resized or made
    /// fullscreen: the frame is stretched to whatever size the window is now.
    /// </summary>
    private void FollowWindowSize()
    {
        if (!SDL.SDL_GetWindowSize(MainWindow.Handle, out int width, out int height) || width < 1 || height < 1)
            return;
        Pointer.Scale = new Vector2((float)Width / width, (float)Height / height);
    }

    protected override void Draw(double alpha)
    {
        _probe?.BeginPhase();
        _drawStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        _drawLap = _drawStarted;
        _acquireMs = 0;
        try
        {
            DrawFrame();
        }
        finally
        {
            _probe?.EndDraw();
            // Whatever followed the canvas: the blit, the submit, a shot.
            _phases.Lap(HostFramePhases.Phase.Submit, ref _drawLap);
            _phases.Add(HostFramePhases.Phase.Draw, System.Diagnostics.Stopwatch.GetElapsedTime(_drawStarted).TotalMilliseconds);
            _phases.EndFrame();
            _drawnSinceUpdate = true;
            _autopsy?.AddDraw(System.Diagnostics.Stopwatch.GetElapsedTime(_drawStarted).TotalMilliseconds, _acquireMs);
        }
    }

    private long _drawStarted;
    private long _drawLap;
    private double _acquireMs;
    private long _frameNumber;

    private void DrawFrame()
    {
        Fps.Frame();
        CommandBuffer commands = GraphicsDevice.AcquireCommandBuffer();
        _phases.Lap(HostFramePhases.Phase.AcquireCommands, ref _drawLap);
        long acquire = System.Diagnostics.Stopwatch.GetTimestamp();
        Texture? swapchain = commands.AcquireSwapchainTexture(MainWindow);
        _acquireMs = System.Diagnostics.Stopwatch.GetElapsedTime(acquire).TotalMilliseconds;
        _phases.Lap(HostFramePhases.Phase.WaitSwapchain, ref _drawLap);
        _probe?.EndAcquire();
        if (swapchain is null)
        {
            GraphicsDevice.Submit(commands);
            return;
        }

        GpuTimes.Poll();
        Text.Atlas.Flush();
        _phases.Lap(HostFramePhases.Phase.AtlasFlush, ref _drawLap);
        // "frame" closes before any submit below; GpuTimes counts frames by it.
        GpuScope frame = commands.Scope(GpuTimes.FrameName(_autopsy?.Number ?? _frameNumber++));
        using (commands.Scope("offscreen"))
            RenderOffscreen(commands);
        _phases.Lap(HostFramePhases.Phase.Offscreen, ref _drawLap);

        Color background = Palette.Background;
        using (commands.Scope("canvas"))
            Renderer.Render(commands, Target, new MwColor(background.R, background.G, background.B, 1f), Seconds, _phases);
        _phases.Lap(HostFramePhases.Phase.Canvas, ref _drawLap);
        using (commands.Scope("blit to window"))
            commands.Blit(Target, swapchain, Filter.Linear);
        frame.Dispose();

        _frames++;
        if (_splash is not null)
        {
            _splash.Dispose();
            _splash = null;
        }
        if (_screenshotRequested)
        {
            _screenshotRequested = false;
            SaveScreenshot(commands);
            return;
        }

        bool last = true;
        string? shot = _requestedShot ?? SeriesShot(out last);
        if (shot is { } path)
        {
            GpuTimes.Poll(now: true);
            Console.WriteLine($"Canvas: {Canvas.Count} items, {Renderer.LastBatchCount} draw calls, " +
                $"{Renderer.LastOffscreenCount} offscreen passes, {Renderer.LastCopyCount} screen copies, " +
                $"{Renderer.LastPrimitiveCount} primitives placed, {Renderer.LastUploadCount} sent");
            Screenshot.Save(GraphicsDevice, commands, Target, Width, Height, path);
            if (_requestedShot is not null || last)
                Quit();
            return;
        }

        GraphicsDevice.Submit(commands);
    }

    /// <summary>
    /// The <c>--shot</c> path due this frame, if any. <c>XANADU_SHOT_SERIES=count,step</c>
    /// takes <c>count</c> shots <c>step</c> frames apart from the shot frame, numbered
    /// <c>name_0.png</c> on, and quits after the last; without it, one shot, then quit.
    /// </summary>
    private string? SeriesShot(out bool last)
    {
        last = true;
        if (_window.ShotPath is not { } path)
            return null;

        int count = 1, step = 1;
        if (Environment.GetEnvironmentVariable("XANADU_SHOT_SERIES") is { } series &&
            series.Split(',') is [var c, var st] && int.TryParse(c, out int parsedCount) && int.TryParse(st, out int parsedStep) &&
            parsedCount > 0 && parsedStep > 0)
        {
            count = parsedCount;
            step = parsedStep;
        }

        int since = _frames - _window.ShotFrame;
        if (since < 0 || since % step != 0 || since / step >= count)
            return null;

        int index = since / step;
        last = index == count - 1;
        return count == 1 ? path : Path.Combine(Path.GetDirectoryName(path) ?? "", $"{Path.GetFileNameWithoutExtension(path)}_{index}{Path.GetExtension(path)}");
    }

    /// <summary>F12, as in the Godot build: a PNG in <c>user://screenshots</c>.</summary>
    private void SaveScreenshot(CommandBuffer commands)
    {
        try
        {
            string folder = GamePaths.Full("user://screenshots");
            Directory.CreateDirectory(folder);
            string file = ScreenshotName();
            Screenshot.Save(GraphicsDevice, commands, Target, Width, Height, Path.Combine(folder, file));
            Notify($"Screenshot saved: {file}");
        }
        catch (Exception exception)
        {
            Notify($"Screenshot failed: {exception.Message}");
        }
    }

    protected override void Destroy()
    {
        SaveAutopsy();
        _sampler?.Stop();
        Shutdown();
        _splash?.Dispose();
        _ui?.Dispose();
        Text.Dispose();
        Renderer.Dispose();
        _gpuMemory.Dispose();
    }
}

/// <summary>Frames per second over the last second, for the debug overlay.</summary>
internal sealed class HostFps : IFpsSource
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
