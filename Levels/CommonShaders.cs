namespace Xanadu.Levels;

/// <summary>
/// Shaders every editor and animator may use, independent of any one map or
/// mapset. Shipped as trusted game content, not user data, so they need no
/// source validation the way a map's own shaders do.
/// </summary>
public static class CommonShaders
{
    /// <summary>Resource paths of the game's common canvas shaders.</summary>
    public static readonly string[] Paths =
    {
        "res://Assets/Shaders/Items/rainbow.gdshader",
    };

    public static bool IsKnown(string path) => System.Array.IndexOf(Paths, path) >= 0;
}
