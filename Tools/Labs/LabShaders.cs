using System.IO;
using Xanadu.Slang;

namespace Xanadu.Labs;

/// <summary>SPIR-V for the lab's own shader modules (Tools/Labs/Shaders).</summary>
internal static class LabShaders
{
    /// <summary>The module's path next to the exe.</summary>
    public static string Module(string file) => Path.Combine(AppContext.BaseDirectory, "Shaders", file);

    /// <summary>
    /// SPIR-V for one entry, kept in the shader cache by a hash of the module
    /// and the entry: modules with many entries would stall a tab for seconds
    /// each time it opens if every entry compiled fresh.
    /// </summary>
    public static byte[] Cached(string module, string entry, string stage)
    {
        string source = File.ReadAllText(module);
        if (SlangShaders.CacheFolder is not { } folder)
            return SlangShaders.Compile(module, entry, stage);
        string name = Path.GetFileNameWithoutExtension(module);
        byte[] hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{name}|{entry}|{stage}\n{source}"));
        string path = Path.Combine(folder, name + "-" + Convert.ToHexString(hash)[..32] + ".spv");
        if (File.Exists(path))
            return File.ReadAllBytes(path);
        byte[] spirv = SlangShaders.Compile(module, entry, stage);
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(path, spirv);
        return spirv;
    }
}
