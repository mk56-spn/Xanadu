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

    /// <summary>
    /// This run's shader cache: fresh each run, so everything is compiled once, but a stack the suites compile again
    /// (every shipped one is compiled by several fixtures) is read back. Gone when the run ends.
    /// </summary>
    public static string RunCache { get; } = MakeRunCache();

    private static string MakeRunCache()
    {
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xanadu-slang-tests-" + Guid.NewGuid().ToString("N"));
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        };
        return folder;
    }

    /// <summary>Points the compiler at the library and loads its constants and chains, as the host does at start.</summary>
    public static void UseForCompiling()
    {
        SlangShaders.LibraryPath = Path;
        SlangShaders.CacheFolder = RunCache;
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
