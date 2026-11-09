using Slangc.NET;

namespace Xanadu.Host.Rendering;

/// <summary>Slang to SPIR-V at runtime through Slangc.NET, one entry point at a time.</summary>
internal static class SlangShaders
{
    /// <summary>Compiles one entry point; the SPIR-V entry is named <c>main</c>.</summary>
    public static byte[] Compile(string path, string entry, string stage) =>
        SlangCompiler.Compile([path, "-target", "spirv", "-entry", entry, "-stage", stage]);
}
