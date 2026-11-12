using System;
using System.IO;

namespace Xanadu.Storage;

/// <summary>
/// The one place <c>res://</c> and <c>user://</c> paths become real ones.
/// <c>res://</c> is the game folder and <c>user://</c> the folder Godot used to
/// keep, so old saves stay put. Hosts set them with <see cref="UseRoots"/>;
/// otherwise they are found on first use. Plain paths come back unchanged.
/// </summary>
public static class GamePaths
{
    private static (string Res, string User)? _roots;

    /// <summary>The file that marks the game folder, the one <c>res://</c> means.</summary>
    public const string RootMarker = "Xanadu.sln";

    /// <summary>
    /// The game folder above the running exe (or the working directory), for
    /// when no host has set <see cref="UseRoots"/>, such as tests.
    /// </summary>
    private static string FindGameFolder()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (DirectoryInfo? folder = new(start); folder is not null; folder = folder.Parent)
            {
                if (IsGameFolder(folder.FullName))
                    return folder.FullName;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    /// <summary>Whether <paramref name="folder"/> is the game folder.</summary>
    public static bool IsGameFolder(string folder) => File.Exists(Path.Combine(folder, RootMarker));

    /// <summary>Sets the folders <c>res://</c> and <c>user://</c> resolve to.</summary>
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
        (string Res, string User) roots = _roots ??= (FindGameFolder(), GodotUserFolder("Xanadu"));

        if (path.StartsWith("res://", StringComparison.Ordinal))
            return Path.Combine(roots.Res, path["res://".Length..]);
        if (path.StartsWith("user://", StringComparison.Ordinal))
            return Path.Combine(roots.User, path["user://".Length..]);
        return path;
    }
}
