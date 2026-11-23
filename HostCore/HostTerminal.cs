using System;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Terminal;

namespace Xanadu.Host;

/// <summary>
/// The commands every host exe's terminal has: the window and how frames
/// are paced. They act on the running window only; the game's settings
/// screen is still what saves a choice.
/// </summary>
internal static class HostTerminal
{
    private static readonly string[] Modes = ["windowed", "fullscreen", "borderless"];
    private static readonly WindowMode[] WindowModes = [WindowMode.Windowed, WindowMode.Fullscreen, WindowMode.Borderless];

    public static void AddCommands(HostApp host)
    {
        TerminalCommands commands = host.Commands;

        commands.Add(new TerminalCommand("vsync", "Shows vertical sync, or turns it on or off",
            [TerminalArg.Switch("state", optional: true)], args =>
            {
                if (args.Has(0))
                    host.VSync = args.Switch(0);
                return VSyncState(host);
            }));

        commands.Add(new TerminalCommand("maxfps", "Caps frames per second while vsync is off (0: no cap)",
            [TerminalArg.Whole("frames", optional: true)], args =>
            {
                if (args.Has(0))
                    host.MaxFps = (int)Math.Clamp(args.Whole(0), 0, 10_000);
                string cap = host.MaxFps > 0 ? $"capped at {host.MaxFps}" : "not capped";
                return host.VSync ? $"vsync is on, so frames follow the display; with it off they'd be {cap}." : $"frames are {cap}.";
            }));

        commands.Add(new TerminalCommand("fps", "Frames per second now, and how they're paced",
            [], _ => $"{host.CurrentFps:0} fps · {VSyncState(host)} · display {host.DisplayHz} Hz"));

        commands.Add(new TerminalCommand("window", "Puts the window in a window, fullscreen, or borderless over the display",
            [TerminalArg.Choice("mode", Modes)], args =>
            {
                host.SetWindowMode(WindowModes[args.Choice(0)]);
                return "window: " + Modes[args.Choice(0)];
            }));

        commands.Add(new TerminalCommand("screenshot", "Saves the next frame to user://screenshots (as F12)",
            [], _ =>
            {
                host.RequestScreenshot();
                return "";
            }));

        commands.Add(new TerminalCommand("quit", "Closes the window", [], _ =>
        {
            host.Quit();
            return "";
        }));
    }

    private static string VSyncState(HostApp host) => host.VSync
        ? $"vsync on ({host.DisplayHz} Hz)"
        : "vsync off" + (host.MaxFps > 0 ? $", capped at {host.MaxFps}" : ", uncapped");
}
