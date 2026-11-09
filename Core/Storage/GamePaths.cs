using System;
using System.IO;
using ProjectSettings = Godot.ProjectSettings;

namespace Xanadu.Storage;

/// <summary>
/// The one place <c>res://</c> and <c>user://</c> paths become real ones.
/// Godot resolves them itself; the MoonWorks host has no engine, so it sets
/// <see cref="Roots"/> to the game folder and the same user folder Godot uses
/// (docs/moonworks-port.md step 2). Plain paths come back unchanged.
/// </summary>
public static class GamePaths
{
    private static (string Res, string User)? _roots;

    /// <summary>The file that marks the game folder, the one <c>res://</c> means.</summary>
    public const string RootMarker = "Xanadu.csproj";

    /// <summary>Whether <paramref name="folder"/> is the game folder.</summary>
    public static bool IsGameFolder(string folder) => File.Exists(Path.Combine(folder, RootMarker));

    /// <summary>Use these folders instead of asking Godot.</summary>
    public static void UseRoots(string res, string user) => _roots = (res, user);

    /// <summary>The folder Godot keeps <c>user://</c> in for this game, on Linux.</summary>
    public static string GodotUserFolder(string gameName) =>
        Path.Combine(
            Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } data
                ? data
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share"),
            "godot",
            "app_userdata",
            gameName);

    public static string Globalize(string path)
    {
        if (_roots is not { } roots)
            return ProjectSettings.GlobalizePath(path);

        if (path.StartsWith("res://", StringComparison.Ordinal))
            return Path.Combine(roots.Res, path["res://".Length..]);
        if (path.StartsWith("user://", StringComparison.Ordinal))
            return Path.Combine(roots.User, path["user://".Length..]);
        return path;
    }
}
