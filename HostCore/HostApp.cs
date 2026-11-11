using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Friflo.Engine.ECS;
using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Input;
using Xanadu.Ecs;
using Xanadu.Host.Input;
using Xanadu.Host.Rendering;
using Xanadu.Host.Text;
using Xanadu.Storage;
using Xanadu.Systems;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;
using MwColor = MoonWorks.Graphics.Color;

namespace Xanadu.Host;

/// <summary>Window size and the <c>--shot</c> settings every host exe takes.</summary>
internal sealed record HostWindowOptions(uint Width, uint Height, string? ShotPath, int ShotFrame);

/// <summary>
/// What every MoonWorks exe shares (Docs/host.md): the window, the
/// 1920×1080 frame drawn by the host's canvas renderer and blitted to the
/// window, fonts, keyboard, pointer and text input, the shader library, vsync,
/// F12 screenshots and <c>--shot</c>. The game and the tools derive from it and
/// put their ECS world on <see cref="Canvas"/> in <see cref="Tick"/>.
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
    private readonly Autopsy.AutopsyRecorder? _autopsy;
    private readonly Autopsy.CpuSampler? _sampler;

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "gettid")]
    private static extern int GetThreadId();
    private bool _drawnSinceUpdate = true;

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

    /// <summary>Seconds since the window opened: the TIME effects see.</summary>
    protected float Seconds => (float)(DateTime.UtcNow - _started).TotalSeconds;

    /// <summary>Runs before any instance's base constructor makes the graphics device, which is when the layer must be in place.</summary>
    static HostApp() => GpuTimes.Install();

    protected HostApp(string title, HostWindowOptions window) : base(
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
        SlangShaders.LibraryPath = GamePaths.Globalize("res://Assets/Shaders/Library");
        Xanadu.Rendering.ShaderConstants.Use(SlangShaders.LoadConstants());
        Xanadu.Rendering.ShaderChains.Use(SlangShaders.LoadChains());
        SlangShaders.CacheFolder = GamePaths.Globalize("user://shader_cache_slang");
        Renderer = new CanvasRenderer(GraphicsDevice, Canvas, TargetFormat, Width, Height);
        Text = new HostText(GraphicsDevice, texture =>
        {
            ulong id = Canvas.MintRid().Id;
            Renderer.Textures[id] = texture;
            return id;
        });

        Keyboard = new HostKeyboard(Inputs.Keyboard);
        Pointer = new HostPointer(Inputs.Mouse, Keyboard)
        {
            Scale = new Vector2((float)Width / window.Width, (float)Height / window.Height),
        };
        HostPlatform.Use(Canvas, () => new HostRectCanvas(Text), Keyboard, new CanvasServerRidFactory(loadsAssets: true), Fps);
        HostPlatform.Particles = Renderer.Particles;
        HostPlatform.GpuMemory = _gpuMemory;
        TextureServer.Use(new HostTextures(GraphicsDevice, Canvas, Renderer));
        UiIcons.Loader = UiIcons.FromResources;
        HostPlatform.GlyphStrip = atlas =>
        {
            Handle rid = Canvas.MintRid();
            Renderer.Textures[rid.Id] = GlyphStrips.Build(GraphicsDevice, Text, atlas);
            return rid;
        };
        Renderer.Pipelines.WarmShipped();
        HostPlatform.DescribeMaterial = DescribeMaterial;
        HostPlatform.ApplyVSync = ApplyVSync;
        ApplyVSync(true);
        HostPlatform.ApplyWindowMode = ApplyWindowMode;
        TextInput = new HostTextInput(Entities, MainWindow);
        HostPlatform.TextEntry = TextInput;
        HostPlatform.MeasureMono = (text, size) => Text.Width(text, Xanadu.Host.Text.FontKind.Mono, size);
        _probe = HostFrameProbe.Enabled ? new HostFrameProbe() : null;

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

    /// <summary>The systems the frame autopsy times, one by one; null leaves update as one block.</summary>
    protected virtual Friflo.Engine.ECS.Systems.SystemRoot? Systems => null;

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

    /// <summary>Runs the exe's own systems once a frame, with real seconds since the last frame.</summary>
    protected abstract void Tick(float delta, float time);

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
        PresentMode mode = enabled ? PresentMode.VSync
            : GraphicsDevice.SupportsPresentMode(MainWindow, PresentMode.Mailbox) ? PresentMode.Mailbox
            : PresentMode.Immediate;
        GraphicsDevice.SetSwapchainParameters(MainWindow, SwapchainComposition.SDR, mode);
        // MoonWorks' Uncapped mode draws without updating between timesteps, so
        // "off" is a cap no frame reaches: every frame still gets one update.
        SetFramePacingSettings(FramePacingSettings.CreateCapped(enabled ? RefreshRate() : 100_000, 1));
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
        uint display = SDL3.SDL.SDL_GetDisplayForWindow(window);
        switch (mode)
        {
            case WindowMode.Fullscreen:
                SDL3.SDL.SDL_SetWindowBordered(window, true);
                SDL3.SDL.SDL_DisplayMode* current = SDL3.SDL.SDL_GetCurrentDisplayMode(display);
                if (current is not null)
                    SDL3.SDL.SDL_SetWindowFullscreenMode(window, ref *current);
                SDL3.SDL.SDL_SetWindowFullscreen(window, true);
                break;
            case WindowMode.Borderless:
                // Desktop fullscreen: no mode of its own, the window covers the
                // display at the desktop's resolution.
                SDL3.SDL.SDL_SetWindowFullscreenMode(window, ref System.Runtime.CompilerServices.Unsafe.NullRef<SDL3.SDL.SDL_DisplayMode>());
                SDL3.SDL.SDL_SetWindowFullscreen(window, true);
                break;
            default:
                SDL3.SDL.SDL_SetWindowFullscreen(window, false);
                SDL3.SDL.SDL_SetWindowBordered(window, true);
                SDL3.SDL.SDL_SetWindowSize(window, (int)_window.Width, (int)_window.Height);
                break;
        }
        SDL3.SDL.SDL_SyncWindow(window);
    }

    private unsafe int RefreshRate()
    {
        SDL3.SDL.SDL_DisplayMode* display = SDL3.SDL.SDL_GetCurrentDisplayMode(SDL3.SDL.SDL_GetDisplayForWindow(MainWindow.Handle));
        return display is null || display->refresh_rate < 1 ? 60 : (int)MathF.Round(display->refresh_rate);
    }

    /// <summary>Points res:// and user:// where every host exe has them; for a command that runs without a window.</summary>
    internal static void UseGameRoots() => GamePaths.UseRoots(FindGameFolder(), GamePaths.GodotUserFolder("Xanadu"));

    /// <summary>The folder <c>res://</c> means: the one holding <see cref="GamePaths.RootMarker"/>, above the exe.</summary>
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

        return effect.Compiled.Params
            .Select(param => new MaterialParam(param.Name, Math.Clamp(param.Size / 4, 1, 4), param.IsInt, param.Default))
            .ToList();
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
        _probe?.BeginPhase();
        TextInput.Update(Inputs.Keyboard, delta);
        if (Inputs.Keyboard.IsPressed(KeyCode.F12))
            _screenshotRequested = true;
        FollowWindowSize();
        Pointer.Poll();
        Tick(delta, (float)(now - _started).TotalSeconds);
        Pointer.EndFrame();
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
        if (!SDL3.SDL.SDL_GetWindowSize(MainWindow.Handle, out int width, out int height) || width < 1 || height < 1)
            return;
        Pointer.Scale = new Vector2((float)Width / width, (float)Height / height);
    }

    protected override void Draw(double alpha)
    {
        _probe?.BeginPhase();
        _drawStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        _acquireMs = 0;
        try
        {
            DrawFrame();
        }
        finally
        {
            _probe?.EndDraw();
            _drawnSinceUpdate = true;
            _autopsy?.AddDraw(System.Diagnostics.Stopwatch.GetElapsedTime(_drawStarted).TotalMilliseconds, _acquireMs);
        }
    }

    private long _drawStarted;
    private double _acquireMs;
    private long _frameNumber;

    private void DrawFrame()
    {
        Fps.Frame();
        CommandBuffer commands = GraphicsDevice.AcquireCommandBuffer();
        long acquire = System.Diagnostics.Stopwatch.GetTimestamp();
        Texture? swapchain = commands.AcquireSwapchainTexture(MainWindow);
        _acquireMs = System.Diagnostics.Stopwatch.GetElapsedTime(acquire).TotalMilliseconds;
        _probe?.EndAcquire();
        if (swapchain is null)
        {
            GraphicsDevice.Submit(commands);
            return;
        }

        GpuTimes.Poll();
        Text.Atlas.Flush();
        // "frame" closes before any submit below; GpuTimes counts frames by it.
        GpuScope frame = commands.Scope(GpuTimes.FrameName(_autopsy?.Number ?? _frameNumber++));
        using (commands.Scope("offscreen"))
            RenderOffscreen(commands);

        Xanadu.Maths.Color background = Palette.Background;
        using (commands.Scope("canvas"))
            Renderer.Render(commands, Target, new MwColor(background.R, background.G, background.B, 1f), Seconds);
        using (commands.Scope("blit to window"))
            commands.Blit(Target, swapchain, Filter.Linear);
        frame.Dispose();

        _frames++;
        if (_screenshotRequested)
        {
            _screenshotRequested = false;
            SaveScreenshot(commands);
            return;
        }

        string? shot = _requestedShot ?? (_frames == _window.ShotFrame ? _window.ShotPath : null);
        if (shot is { } path)
        {
            GpuTimes.Poll(now: true);
            Console.WriteLine($"Canvas: {Canvas.Count} items, {Renderer.LastBatchCount} draw calls, " +
                $"{Renderer.LastOffscreenCount} offscreen passes, {Renderer.LastCopyCount} screen copies");
            Screenshot.Save(GraphicsDevice, commands, Target, Width, Height, path);
            Quit();
            return;
        }

        GraphicsDevice.Submit(commands);
    }

    /// <summary>F12, as in the Godot build: a PNG in <c>user://screenshots</c>.</summary>
    private void SaveScreenshot(CommandBuffer commands)
    {
        try
        {
            string folder = GamePaths.Globalize("user://screenshots");
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
        Text.Dispose();
        Renderer.Dispose();
        _gpuMemory.Dispose();
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
