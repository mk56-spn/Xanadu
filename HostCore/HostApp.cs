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
/// What every MoonWorks exe shares (docs/moonworks-port.md): the window, the
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

    protected Texture Target { get; }
    protected CanvasTree Canvas { get; } = new();
    protected CanvasRenderer Renderer { get; }
    protected HostText Text { get; }
    protected HostKeyboard Keyboard { get; }
    protected HostPointer Pointer { get; }
    protected HostTextInput TextInput { get; }
    protected HostFps Fps { get; } = new();
    protected EntityStore Entities { get; } = new();

    /// <summary>Seconds since the window opened: the TIME effects see.</summary>
    protected float Seconds => (float)(DateTime.UtcNow - _started).TotalSeconds;

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
        GamePaths.UseRoots(FindGameFolder(), GamePaths.GodotUserFolder("Xanadu"));
        SlangShaders.LibraryPath = GamePaths.Globalize("res://Assets/Shaders/Library");
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
            Scale = new Godot.Vector2((float)Width / window.Width, (float)Height / window.Height),
        };
        HostPlatform.Use(Canvas, () => new HostRectCanvas(Text), Keyboard, new CanvasServerRidFactory(loadsAssets: true), Fps);
        HostPlatform.Particles = Renderer.Particles;
        TextureServer.Use(new HostTextures(GraphicsDevice, Canvas, Renderer));
        UiIcons.Loader = UiIcons.FromResources;
        HostPlatform.GlyphStrip = atlas =>
        {
            Godot.Rid rid = Canvas.MintRid();
            Renderer.Textures[rid.Id] = GlyphStrips.Build(GraphicsDevice, Text, atlas);
            return rid;
        };
        Renderer.Pipelines.WarmShipped();
        HostPlatform.DescribeMaterial = DescribeMaterial;
        HostPlatform.ApplyVSync = ApplyVSync;
        ApplyVSync(true);
        TextInput = new HostTextInput(Entities, MainWindow);
    }

    /// <summary>Saves the next frame to <paramref name="path"/> and quits, for an exe that times its own shot.</summary>
    protected void RequestShot(string path) => _requestedShot = path;

    /// <summary>Runs the exe's own systems once a frame, with real seconds since the last frame.</summary>
    protected abstract void Tick(float delta, float time);

    /// <summary>Where an F12 shot goes, and its name; the game uses its own naming.</summary>
    protected virtual string ScreenshotName() => $"Xanadu {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png";

    /// <summary>Tells the user about a screenshot; the game shows a toast.</summary>
    protected virtual void Notify(string message) => Console.WriteLine(message);

    /// <summary>Called when the window closes, before the host's own resources go.</summary>
    protected virtual void Shutdown() { }

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

        TextInput.Update(Inputs.Keyboard, delta);
        if (Inputs.Keyboard.IsPressed(KeyCode.F12))
            _screenshotRequested = true;
        Pointer.Poll();
        Tick(delta, (float)(now - _started).TotalSeconds);
        Pointer.EndFrame();
    }

    protected override void Draw(double alpha)
    {
        Fps.Frame();
        CommandBuffer commands = GraphicsDevice.AcquireCommandBuffer();
        Texture? swapchain = commands.AcquireSwapchainTexture(MainWindow);
        if (swapchain is null)
        {
            GraphicsDevice.Submit(commands);
            return;
        }

        Text.Atlas.Flush();

        // Palette comes from the UI assembly, and its Godot Color struct works without the engine.
        Godot.Color background = Palette.Background;
        Renderer.Render(commands, Target, new MwColor(background.R, background.G, background.B, 1f), Seconds);
        commands.Blit(Target, swapchain, Filter.Linear);

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
        Shutdown();
        Text.Dispose();
        Renderer.Dispose();
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
