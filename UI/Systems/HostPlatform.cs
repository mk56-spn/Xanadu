using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Debug;
using Xanadu.Systems.Input;

namespace Xanadu.Systems;

/// <summary>
/// What the running host provides that systems fall back on when nothing is
/// passed in: headless stand-ins (no keys down, a canvas nobody sees) until the
/// MoonWorks host calls <see cref="Use"/> before building the world
/// (docs/host.md §5.2). The canvas itself is <see cref="CanvasServer"/>.
/// </summary>
public static class HostPlatform
{
    public static IKeyboardState Keyboard { get; private set; } = new NoKeyboard();

    /// <summary>
    /// The width the host's monospaced face really draws a text at a size, from
    /// its font; null where no host has fonts (tests), and then estimates stand in.
    /// </summary>
    public static System.Func<string, int, float>? MeasureMono { get; set; }

    /// <summary>Typing and the clipboard for UI that edits text itself; the host's once it has a window.</summary>
    public static ITextEntry TextEntry { get; set; } = new NoTextEntry();

    /// <summary>Makes canvas items; only the host's loads textures and fonts into the UI.</summary>
    public static IVisualRidFactory Visuals { get; private set; } = new CanvasServerRidFactory();

    public static IFpsSource Fps { get; private set; } = new NoFps();

    /// <summary>Video memory use for the F3 overlay; null where nothing can read it (tests).</summary>
    public static IGpuMemorySource? GpuMemory { get; set; }

    private static System.Func<IRectCanvas>? _newCanvas;

    /// <summary>A canvas for a system root to draw through: the host's, with its text, else shapes only.</summary>
    public static IRectCanvas NewCanvas() => _newCanvas?.Invoke() ?? new ServerRectCanvas();

    /// <summary>Turns the window's vertical sync on or off.</summary>
    public static System.Action<bool> ApplyVSync { get; set; } = static _ => { };

    /// <summary>Puts the window in a decorated window, fullscreen, or a borderless window covering the display.</summary>
    public static System.Action<WindowMode> ApplyWindowMode { get; set; } = static _ => { };

    /// <summary>
    /// Builds a glyph strip's texture and returns its canvas texture id; null
    /// where nothing draws (tests), and then strips have no texture.
    /// </summary>
    public static System.Func<GlyphAtlas, Handle>? GlyphStrip { get; set; }

    /// <summary>
    /// The values a material's shader declares, from the host's compiler; null
    /// when the shader can't be built or no host describes materials (tests).
    /// </summary>
    public static System.Func<CanvasMaterial, System.Collections.Generic.IReadOnlyList<MaterialParam>?>? DescribeMaterial { get; set; }

    /// <summary>The host's particles (docs/host.md §5.6); null in tests.</summary>
    public static Xanadu.Particles.IParticleServer? Particles { get; set; }

    public static void Use(
        ICanvasServer server,
        System.Func<IRectCanvas> newCanvas,
        IKeyboardState keyboard,
        IVisualRidFactory visuals,
        IFpsSource fps)
    {
        CanvasServer.Use(server);
        _newCanvas = newCanvas;
        Keyboard = keyboard;
        Visuals = visuals;
        Fps = fps;
    }
}

/// <summary>A keyboard with nothing ever pressed.</summary>
public sealed class NoKeyboard : IKeyboardState
{
    public bool IsPressed(Key key) => false;
}

/// <summary>A frame rate nobody measures.</summary>
public sealed class NoFps : IFpsSource
{
    public double GetFps() => 0;
}

/// <summary>How the game's window sits on the display.</summary>
public enum WindowMode
{
    /// <summary>A normal decorated window.</summary>
    Windowed,

    /// <summary>Exclusive fullscreen at the display's current mode.</summary>
    Fullscreen,

    /// <summary>Borderless windowed: a window covering the whole display at the desktop resolution, no mode change.</summary>
    Borderless,
}
