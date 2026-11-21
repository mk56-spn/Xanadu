using System.IO;
using Xanadu.Core.Storage;
using Xanadu.Model.Animation;

namespace Xanadu.Storage.Animation;

/// <summary>Rig art from disk: game paths resolved through <see cref="GamePaths"/>. Installed into <see cref="SvgArt"/> at start-up.</summary>
public sealed class SvgArtFiles : ISvgArtSource
{
    public string? Read(string svgPath)
    {
        string path = GamePaths.Full(svgPath);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}
