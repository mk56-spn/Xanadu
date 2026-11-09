using System;

namespace Xanadu.Diagnostics;

/// <summary>
/// Where shared code reports what went wrong: the console by default
/// (stderr for problems). A host may route these elsewhere.
/// </summary>
public static class GameLog
{
    public static Action<string> Info { get; set; } = Console.WriteLine;

    public static Action<string> Warning { get; set; } = message => Console.Error.WriteLine($"Warning: {message}");

    public static Action<string> Error { get; set; } = message => Console.Error.WriteLine($"Error: {message}");
}
