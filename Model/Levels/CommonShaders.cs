using System.Collections.Generic;

namespace Xanadu.Model.Levels;

/// <summary>Lists the shader stacks on disk; Storage provides it (<c>GameFiles.Install</c>).</summary>
public interface IShaderStackFolders
{
    /// <summary>Every stack's resource path (<c>Assets/Shaders/Game/orb</c>), library left out, in order.</summary>
    IReadOnlyList<string> List();
}

/// <summary>
/// The game's own shaders: every shader stack under <see cref="Root"/> outside
/// the library, any of which a node may draw with (plain, deform, pool or full
/// graph alike). <see cref="Paths"/> and <see cref="Layers"/> are only the ones the
/// editors offer in their menus; the game draws whichever path a node names.
/// </summary>
public static class CommonShaders
{
    /// <summary>Where the game's shader stacks live; each is a folder below it.</summary>
    private const string Root = "Assets/Shaders/";

    /// <summary>The shader library's folder: modules imported by stacks, never a stack drawn on its own.</summary>
    private const string Library = Root + "Library/";

    /// <summary>The shaders the editors offer, by resource path.</summary>
    public static readonly string[] Paths =
    [
        "Assets/Shaders/Game/rainbow",
        Playfields.Decos.FlareShader,
        Playfields.DashStops.Shader,
        Playfields.Pads.Shader,
        Playfields.Drafts.Shader,
        Playfields.Orbs.Shader,
        Playfields.Beams.Shader,
        Playfields.Rifts.Shader,
        Playfields.Rifts.FlareShader,
        Playfields.NoteLooks.NoteShader,
        Playfields.NoteLooks.DashShader,
        Playfields.NoteLooks.LineShader,
        Playfields.NoteLooks.DotShader,
        Playfields.NoteLooks.PopShader,
        Playfields.NoteLooks.DashPopShader,
        "Assets/Shaders/Game/sdf",
    ];

    /// <summary>
    /// Small common shaders made to be stacked over others (<c>XifShader.Layers</c>):
    /// each does one thing to the shape or the colour so far. Any common shader can
    /// be a layer; these are the ones the editor offers first.
    /// </summary>
    public static readonly string[] Layers =
    [
        SwayLayer,
        GradientLayer,
        TintLayer,
        DissolveLayer,
    ];

    public const string SwayLayer = "Assets/Shaders/Layers/sway";
    public const string GradientLayer = "Assets/Shaders/Layers/gradient";
    public const string TintLayer = "Assets/Shaders/Layers/tint";
    public const string DissolveLayer = "Assets/Shaders/Layers/dissolve";

    private static IShaderStackFolders? _folders;

    /// <summary>Hooks in what lists the stacks on disk; until then <see cref="All"/> is the editors' list.</summary>
    public static void Use(IShaderStackFolders folders) => _folders = folders;

    /// <summary>Every stack the game has, by path, asked fresh each time (the shader tool may have added one).</summary>
    public static IReadOnlyList<string> All() => _folders?.List() ?? [.. Paths, .. Layers];

    /// <summary>
    /// The folder a stack is grouped under: its first folder below the shaders folder
    /// (<c>Game</c>, <c>Lab</c>, <c>Learn</c>…), or <see cref="Loose"/> for one sitting right in it.
    /// </summary>
    public static string CategoryOf(string path)
    {
        if (!IsKnown(path))
            return Loose;
        string rest = path[Root.Length..];
        int slash = rest.IndexOf('/');
        return slash < 0 ? Loose : rest[..slash];
    }

    /// <summary>What <see cref="CategoryOf"/> calls a stack with no folder of its own.</summary>
    public const string Loose = "Other";

    /// <summary>A stack's own name: its folder's last part.</summary>
    public static string NameOf(string path) => path[(path.LastIndexOf('/') + 1)..];

    /// <summary>
    /// Whether a path names one of the game's shader stacks: a folder under <see cref="Root"/>, not the library.
    /// Whether the folder holds a stack is the renderer's to find out; a missing one draws plain.
    /// </summary>
    public static bool IsKnown(string path) =>
        path.Length > Root.Length
        && path.StartsWith(Root, System.StringComparison.Ordinal)
        && !path.StartsWith(Library, System.StringComparison.Ordinal)
        && !path.Contains("..", System.StringComparison.Ordinal)
        && !path.Contains('\\')
        && !path.EndsWith('/');
}
