using Xanadu.Core.Particles;
using Xanadu.Model.Levels;
using Xanadu.Storage.Particles;
using Xanadu.Storage.Shaders;

namespace Xanadu.Storage;

/// <summary>
/// Hooks the game's files into the lower layers that only declare what they
/// need: the particle library's file (Core) and the list of shader stacks
/// (Model). Every app calls <see cref="Install"/> once, before it builds anything.
/// </summary>
public static class GameFiles
{
    public static void Install()
    {
        ParticleLibrary.Use(new ParticleLibraryFile());
        CommonShaders.Use(new ShaderStackFolders());
    }
}
