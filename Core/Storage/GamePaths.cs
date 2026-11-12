using System;
using System.IO;

namespace Xanadu.Storage;

/// <summary>
/// The one place paths become real ones. A relative path is under the game
/// folder (<c>Assets/Shaders/flare.slang</c>); <c>user://</c> is the player's
/// folder (<c>~/.local/share/xanadu</c>); an absolute path comes
/// back unchanged. Hosts set the folders with <see cref="UseRoots"/>; otherwise
/// they are found on first use.
/// </summary>
public static class GamePaths
{
    private const string UserPrefix = "user://";

    private static (string Game, string User)? _roots;

    /// <summary>The file that marks the game folder.</summary>
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

    private static (string Game, string User) Roots => _roots ??= (FindGameFolder(), UserFolder("xanadu"));

    /// <summary>Whether <paramref name="folder"/> is the game folder.</summary>
    public static bool IsGameFolder(string folder) => File.Exists(Path.Combine(folder, RootMarker));

    /// <summary>Sets the game folder (relative paths) and the folder <c>user://</c> means.</summary>
    public static void UseRoots(string game, string user) => _roots = (game, user);

    /// <summary>The folder relative paths are under.</summary>
    public static string GameFolder => Roots.Game;

    /// <summary>A player's folder by name under the data home (<c>$XDG_DATA_HOME</c>, else <c>~/.local/share</c>).</summary>
    public static string UserFolder(string name) =>
        Path.Combine(
            Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } data
                ? data
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share"),
            name);

    /// <summary>The real path: a relative one under the game folder, <c>user://</c> under the player's, an absolute one as it is.</summary>
    public static string Full(string path)
    {
        if (path.StartsWith(UserPrefix, StringComparison.Ordinal))
            return Path.Combine(Roots.User, path[UserPrefix.Length..]);
        return Path.IsPathRooted(path) ? path : Path.Combine(Roots.Game, path);
    }

    /// <summary>A file under the game folder as the relative path the game names it by (forward slashes).</summary>
    public static string InGame(string file) => Path.GetRelativePath(Roots.Game, Path.GetFullPath(file)).Replace('\\', '/');
}
