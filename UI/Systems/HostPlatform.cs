using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Debug;
using Xanadu.Systems.Input;

namespace Xanadu.Systems;

/// <summary>
/// What the running host provides that systems fall back on when nothing is
/// passed in: Godot's by default, the MoonWorks host's once it calls
/// <see cref="Use"/> before building the world (docs/moonworks-port.md step 2).
/// The canvas itself is <see cref="CanvasServer"/>.
/// </summary>
public static class HostPlatform
{
    public static IKeyboardState Keyboard { get; private set; } = new GodotKeyboardState();

    /// <summary>Makes canvas items. Only Godot's loads Godot textures and fonts into the UI.</summary>
    public static IVisualRidFactory Visuals { get; private set; } = new GodotVisualRidFactory();

    public static IFpsSource Fps { get; private set; } = new GodotFpsSource();

    private static System.Func<IRectCanvas>? _newCanvas;

    /// <summary>A canvas for a system root to draw through: Godot's, with its fonts, unless the host gave its own.</summary>
    public static IRectCanvas NewCanvas() => _newCanvas?.Invoke() ?? new GodotRectCanvas();

    /// <summary>Turns the window's vertical sync on or off.</summary>
    public static System.Action<bool> ApplyVSync { get; set; } = static enabled =>
        Godot.DisplayServer.WindowSetVsyncMode(
            enabled ? Godot.DisplayServer.VSyncMode.Enabled : Godot.DisplayServer.VSyncMode.Disabled);

    /// <summary>
    /// Builds a glyph strip's texture and returns its canvas texture id; null
    /// under Godot, which builds it from its own fonts (<see cref="GlyphAtlas.Texture"/>).
    /// </summary>
    public static System.Func<GlyphAtlas, Godot.Rid>? GlyphStrip { get; set; }

    /// <summary>True under the Godot engine, where Godot-only paths (shader materials, particles, fonts) can run.</summary>
    public static bool IsGodot => CanvasServer.Current is GodotCanvasServer;

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
