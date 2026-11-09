using System;

namespace Xanadu.Diagnostics;

/// <summary>
/// Where shared code reports what went wrong, without touching an engine: the
/// MoonWorks host and tests write to the console (stderr for problems), and the
/// Godot build routes these to <c>GD.Print</c> / <c>GD.PushWarning</c> /
/// <c>GD.PushError</c> in <c>Main</c> (docs/moonworks-port.md step 10).
/// </summary>
public static class GameLog
{
    public static Action<string> Info { get; set; } = Console.WriteLine;

    public static Action<string> Warning { get; set; } = message => Console.Error.WriteLine($"Warning: {message}");

    public static Action<string> Error { get; set; } = message => Console.Error.WriteLine($"Error: {message}");
}
