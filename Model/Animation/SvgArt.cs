namespace Xanadu.Model.Animation;

/// <summary>Where rig art comes from: SVG text by its game path. Storage provides the files.</summary>
public interface ISvgArtSource
{
    /// <summary>The SVG at <paramref name="svgPath"/> (a game path), or null when there is none.</summary>
    string? Read(string svgPath);
}

/// <summary>
/// The art source rigs draw from, installed by the app at start-up. Without
/// one, attachments have no art and draw nothing, as with a missing file.
/// </summary>
public static class SvgArt
{
    private static ISvgArtSource? _source;

    public static void Use(ISvgArtSource source) => _source = source;

    /// <summary>The SVG at <paramref name="svgPath"/>, or null when there is none or no source is installed.</summary>
    public static string? Read(string svgPath) => _source?.Read(svgPath);
}
