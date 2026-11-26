using MoonWorks.Input;
using SDL3;
using Xanadu.Core.Maths;
using Xanadu.Host.Input;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Host;

/// <summary>
/// The host's own keys, read each frame before the systems, in every app: F12 saves a screenshot, F11 puts the window
/// in borderless fullscreen over the display or back in a window.
/// </summary>
internal static class HostKeys
{
    /// <param name="textClaimed">A code editor has the keys: F12 is its go-to-declaration then.</param>
    public static void Poll(HostApp host, Keyboard keyboard, bool textClaimed)
    {
        if (Pressed(keyboard, DebugKeys.Screenshot) && !textClaimed)
            host.RequestScreenshot();
        if (Pressed(keyboard, DebugKeys.Fullscreen))
            host.SetWindowMode(Fullscreen(host) ? WindowMode.Windowed : WindowMode.Borderless);
    }

    private static bool Pressed(Keyboard keyboard, Key key) => HostKeyboard.ToKeyCode(key) is { } code && keyboard.IsPressed(code);

    /// <summary>From the window itself, so it holds whatever set the mode: settings, the terminal or this key.</summary>
    private static bool Fullscreen(HostApp host) =>
        (SDL.SDL_GetWindowFlags(host.MainWindow.Handle) & SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN) != 0;
}
