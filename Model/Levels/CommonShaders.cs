namespace Xanadu.Model.Levels;

/// <summary>
/// Shaders every editor and animator may use, independent of any one map or
/// mapset. Shipped as trusted game content, not user data, so they need no
/// source validation the way a map's own shaders do.
/// </summary>
public static class CommonShaders
{
    /// <summary>Resource paths of the game's common canvas shaders.</summary>
    public static readonly string[] Paths =
    [
        "Assets/Shaders/Items/rainbow.slang",
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
        "Assets/Shaders/Items/sdf.slang",
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

    public const string SwayLayer = "Assets/Shaders/Layers/sway.slang";
    public const string GradientLayer = "Assets/Shaders/Layers/gradient.slang";
    public const string TintLayer = "Assets/Shaders/Layers/tint.slang";
    public const string DissolveLayer = "Assets/Shaders/Layers/dissolve.slang";

    public static bool IsKnown(string path) => System.Array.IndexOf(Paths, path) >= 0 || System.Array.IndexOf(Layers, path) >= 0;
}
