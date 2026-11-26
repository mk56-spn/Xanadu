using System.IO;
using System.Linq;
using System.Text.Json;

namespace Xanadu.Slang;

/// <summary>
/// Compiles kept on disk under <see cref="SlangShaders.CacheFolder"/>: each by a hash of the Slang version, the library,
/// the flags it was compiled with and its source, so editing any of them compiles it again. An entry is its SPIR-V and
/// its reflection side by side.
/// </summary>
internal static class SlangCache
{
    private static string? _libraryHash;

    /// <summary>One hash over every module in the library, so editing one invalidates everything that may import it.</summary>
    public static string LibraryHash => _libraryHash ??= HashLibrary();

    /// <summary>Takes the library's hash again on next use: a library file changed.</summary>
    public static void Forget() => _libraryHash = null;

    /// <summary>Where one compile is kept (no extension: <c>.spv</c> and <c>.json</c> sit there); null without a cache folder.</summary>
    public static string? Path(string source, string entry, string stage, int samplerSpace, int uniformSpace, bool bindGlobals, string[] defines) =>
        SlangShaders.CacheFolder is { } folder
            ? System.IO.Path.Combine(folder, Key(source, $"{entry}|{stage}|{samplerSpace}|{uniformSpace}|{bindGlobals}|{string.Join(';', defines)}"))
            : null;

    public static CompiledShader? Load(string? cached, int samplerSpace, int uniformSpace)
    {
        if (cached is null || !File.Exists(cached + ".spv") || !File.Exists(cached + ".json"))
            return null;
        using var stored = JsonDocument.Parse(File.ReadAllText(cached + ".json"));
        return SlangShaders.Reflect(File.ReadAllBytes(cached + ".spv"), stored.RootElement, samplerSpace, uniformSpace);
    }

    public static void Store(string? cached, CompiledShader compiled, string reflection)
    {
        if (cached is null)
            return;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(cached)!);
        File.WriteAllBytes(cached + ".spv", compiled.Spirv);
        File.WriteAllText(cached + ".json", reflection);
    }

    public static string Key(string source, string flags)
    {
        string version = typeof(Slangc.NET.SlangCompiler).Assembly.GetName().Version?.ToString() ?? "";
        byte[] hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{version}\n{LibraryHash}\n{flags}\n{source}"));
        return Convert.ToHexString(hash)[..32];
    }

    private static string HashLibrary()
    {
        if (SlangShaders.LibraryPath is not { } library || !Directory.Exists(library))
            return "";

        var text = new System.Text.StringBuilder();
        foreach (string file in Directory.EnumerateFiles(library, "*.slang", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            text.Append(System.IO.Path.GetRelativePath(library, file)).Append('\n').Append(File.ReadAllText(file)).Append('\n');
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.ToString())));
    }
}
