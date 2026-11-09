using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Debug;
using Xanadu.Systems.Input;

namespace Xanadu.Systems;

/// <summary>
/// What the running host provides that systems fall back on when nothing is
/// passed in: headless stand-ins (no keys down, a canvas nobody sees) until the
/// MoonWorks host calls <see cref="Use"/> before building the world
/// (docs/moonworks-port.md step 2). The canvas itself is <see cref="CanvasServer"/>.
/// </summary>
public static class HostPlatform
{
    public static IKeyboardState Keyboard { get; private set; } = new NoKeyboard();

    /// <summary>Makes canvas items; only the host's loads textures and fonts into the UI.</summary>
    public static IVisualRidFactory Visuals { get; private set; } = new CanvasServerRidFactory();

    public static IFpsSource Fps { get; private set; } = new NoFps();

    private static System.Func<IRectCanvas>? _newCanvas;

    /// <summary>A canvas for a system root to draw through: the host's, with its text, else shapes only.</summary>
    public static IRectCanvas NewCanvas() => _newCanvas?.Invoke() ?? new ServerRectCanvas();

    /// <summary>Turns the window's vertical sync on or off.</summary>
    public static System.Action<bool> ApplyVSync { get; set; } = static _ => { };

    /// <summary>
    /// Builds a glyph strip's texture and returns its canvas texture id; null
    /// where nothing draws (tests), and then strips have no texture.
    /// </summary>
    public static System.Func<GlyphAtlas, Godot.Rid>? GlyphStrip { get; set; }

    /// <summary>
    /// The values a material's shader declares, from the host's compiler; null
    /// when the shader can't be built or no host describes materials (tests).
    /// </summary>
    public static System.Func<CanvasMaterial, System.Collections.Generic.IReadOnlyList<MaterialParam>?>? DescribeMaterial { get; set; }

    /// <summary>The host's particles (docs/moonworks-port.md step 7); null in tests.</summary>
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
    public bool IsPressed(Godot.Key key) => false;
}

/// <summary>A frame rate nobody measures.</summary>
public sealed class NoFps : IFpsSource
{
    public double GetFps() => 0;
}
