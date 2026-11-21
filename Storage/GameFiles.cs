using Xanadu.Core.Particles;
using Xanadu.Model.Animation;
using Xanadu.Storage.Animation;
using Xanadu.Storage.Particles;

namespace Xanadu.Storage;

/// <summary>
/// Hooks the game's files into the lower layers that only declare what they
/// need: the particle library's file (Core) and rig art (Model). Every app that
/// shows particles or rigs calls <see cref="Install"/> once, before it builds anything.
/// </summary>
public static class GameFiles
{
    public static void Install()
    {
        ParticleLibrary.Use(new ParticleLibraryFile());
        SvgArt.Use(new SvgArtFiles());
    }
}
