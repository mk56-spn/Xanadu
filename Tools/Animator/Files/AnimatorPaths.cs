namespace Xanadu.Tools.Animator.Files;

/// <summary>
/// Where the animator's own content lives, relative to the game folder as
/// every path is in every host exe and in the game, so a package's art paths
/// mean the same file wherever it is loaded.
/// </summary>
public static class AnimatorPaths
{
    /// <summary>The package the animator opens.</summary>
    public const string DefaultPackage = "Tools/Animator/Animations/Characters/contact.xaf";

    /// <summary>Folder swept for art.</summary>
    public const string Assets = "Tools/Animator/Assets";

    /// <summary>Folder the default package's art comes from.</summary>
    public const string Svg = Assets + "/SVG";
}
