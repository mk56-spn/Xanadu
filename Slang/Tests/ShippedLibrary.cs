using System;
using System.IO;
using System.Linq;

namespace Xanadu.Slang.Tests;

/// <summary>
/// The shipped shader library (<c>Assets/Shaders/Library</c>), found from the
/// test's own folder, for the tests that run against the real thing rather
/// than a made-up snippet: compiling, and completion over the real modules.
/// </summary>
internal static class ShippedLibrary
{
    /// <summary>The folder <c>import xanadu.*</c> resolves in.</summary>
    public static string Path { get; } = Find();

    /// <summary>Every module's source.</summary>
    public static string[] Sources { get; } =
        Directory.EnumerateFiles(Path, "*.slang", SearchOption.AllDirectories).Select(File.ReadAllText).ToArray();

    /// <summary>Points the compiler at the library and loads its constants and chains, as the host does at start.</summary>
    public static void UseForCompiling()
    {
        SlangShaders.LibraryPath = Path;
        SlangShaders.CacheFolder = null;
        ShaderConstants.Use(SlangShaders.LoadConstants());
        ShaderChains.Use(SlangShaders.LoadChains());
    }

    private static string Find()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            string library = System.IO.Path.Combine(folder.FullName, "Assets", "Shaders", "Library");
            if (File.Exists(System.IO.Path.Combine(folder.FullName, "Xanadu.sln")) && Directory.Exists(library))
                return library;
        }

        throw new DirectoryNotFoundException("No Assets/Shaders/Library above " + AppContext.BaseDirectory);
    }
}
