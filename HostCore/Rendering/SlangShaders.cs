using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Slangc.NET;

namespace Xanadu.Host.Rendering;

/// <summary>One value an effect reads from its <c>Params</c> block: where it sits, how big, and its default.</summary>
internal sealed record ShaderParam(string Name, int Offset, int Size, bool IsInt, Vector4 Default);

/// <summary>
/// A compiled entry point and what reflection says it binds: how many
/// samplers and uniform buffers (for <c>ShaderCreateInfo</c>), the sampler
/// names by binding, and the layout of uniform buffer 1 (an effect's values).
/// </summary>
internal sealed class CompiledShader(byte[] spirv, string[] samplers, uint uniformBuffers, ShaderParam[] parameters, int paramsSize)
{
    public byte[] Spirv { get; } = spirv;

    /// <summary>Sampler names by binding index in the stage's sampler set; a gap is null.</summary>
    public string[] Samplers { get; } = samplers;

    public uint UniformBuffers { get; } = uniformBuffers;

    public ShaderParam[] Params { get; } = parameters;

    /// <summary>Bytes in the values block, padded to 16; 0 when the effect has none.</summary>
    public int ParamsSize { get; } = paramsSize;

    /// <summary>
    /// Whether the compiled code really samples the screen copy (fragment
    /// sampler 1). Reflection lists every declared global, used or not; the
    /// SPIR-V keeps only what the entry point reaches.
    /// </summary>
    public bool ReadsScreen { get; } = SpirvScan.Binds(spirv, set: 2, binding: 1);
}

/// <summary>Just enough SPIR-V reading to see which resources survived compilation.</summary>
internal static class SpirvScan
{
    private const int OpDecorate = 71, DecorationBinding = 33, DecorationDescriptorSet = 34;

    /// <summary>Whether any variable is decorated with this descriptor set and binding.</summary>
    public static bool Binds(byte[] code, int set, int binding)
    {
        if (code.Length < 20)
            return false;

        var sets = new Dictionary<uint, uint>();
        var bindings = new Dictionary<uint, uint>();
        ReadOnlySpan<uint> words = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(code);
        for (int i = 5; i < words.Length;)
        {
            int count = (int)(words[i] >> 16), opcode = (int)(words[i] & 0xFFFF);
            if (count == 0)
                break;
            if (opcode == OpDecorate && count >= 4)
            {
                if (words[i + 2] == DecorationDescriptorSet)
                    sets[words[i + 1]] = words[i + 3];
                else if (words[i + 2] == DecorationBinding)
                    bindings[words[i + 1]] = words[i + 3];
            }

            i += count;
        }

        foreach ((uint id, uint value) in bindings)
            if (value == binding && sets.TryGetValue(id, out uint s) && s == set)
                return true;
        return false;
    }
}

/// <summary>
/// Slang to SPIR-V at runtime through Slangc.NET, one entry point at a time,
/// with the shipped library (<c>Assets/Shaders/Library</c>: <c>xanadu.stage</c>,
/// <c>xanadu.colour</c>, …) on the import path. The SPIR-V entry is named <c>main</c>.
/// </summary>
internal static class SlangShaders
{
    /// <summary>Where <c>import xanadu.*</c> resolves; set by the host once it knows the game folder.</summary>
    public static string? LibraryPath { get; set; }

    /// <summary>
    /// Where compiled shaders are kept between runs (SPIR-V plus reflection),
    /// keyed by a hash of the source, the entry, the flags, every library module
    /// and the compiler's version; null compiles every time.
    /// </summary>
    public static string? CacheFolder { get; set; }

    private static string? _libraryHash;

    private static readonly string ReflectionFolder =
        Path.Combine(Path.GetTempPath(), $"xanadu-slang-{System.Environment.ProcessId}");

    /// <summary>
    /// Compiles a level shader's effect body (docs/host.md §5.5):
    /// its <c>import</c> lines are hoisted, the rest goes in a namespace of its
    /// own, and a generated entry point runs <c>input(v)</c> then its
    /// <c>shade</c>. <c>#line</c> keeps Slang's errors on the author's lines,
    /// under <paramref name="name"/>.
    /// </summary>
    public static CompiledShader CompileEffectBody(string name, string source)
    {
        Directory.CreateDirectory(ReflectionFolder);
        string file = Path.Combine(ReflectionFolder, Guid.NewGuid().ToString("N") + ".slang");
        File.WriteAllText(file, Wrap(name, source));
        try
        {
            return CompileReflected(file, "xanaduFragment", "fragment", samplerSpace: 2, uniformSpace: 3, bindGlobals: true);
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>The generated module around one effect body; a chain would add one namespace per effect.</summary>
    public static string Wrap(string name, string source)
    {
        var imports = new System.Text.StringBuilder();
        var body = new System.Text.StringBuilder();
        foreach (string line in source.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.TrimStart().StartsWith("import ", StringComparison.Ordinal))
            {
                imports.AppendLine(line);
                body.AppendLine();
            }
            else
                body.AppendLine(line);
        }

        string label = name.Replace("\"", "'");
        return $$"""
            import xanadu.stage;
            {{imports}}
            namespace effect0 {
            #line 1 "{{label}}"
            {{body}}
            }
            #line 1 "{{label}} (wrapper)"
            [shader("fragment")]
            float4 xanaduFragment(ItemVaryings v) : SV_Target
            {
                ItemInput i = input(v);
                i.colour = effect0::shade(i);
                return i.colour;
            }
            """;
    }

    /// <summary>Just the SPIR-V (the particle spike's use).</summary>
    public static byte[] Compile(string path, string entry, string stage) =>
        SlangCompiler.Compile(Arguments(path, entry, stage).ToArray());

    /// <summary>
    /// Compiles and reflects one entry point. <paramref name="samplerSpace"/> and
    /// <paramref name="uniformSpace"/> are the stage's descriptor sets (§3: 2 and 3
    /// for fragment, 0 and 1 for vertex). Throws with Slang's message on an error.
    /// </summary>
    public static CompiledShader CompileReflected(string path, string entry, string stage, int samplerSpace, int uniformSpace, bool bindGlobals = false)
    {
        string? cached = null;
        if (CacheFolder is { } folder)
        {
            string key = CacheKey(File.ReadAllText(path), $"{entry}|{stage}|{samplerSpace}|{uniformSpace}|{bindGlobals}");
            cached = Path.Combine(folder, key);
            if (File.Exists(cached + ".spv") && File.Exists(cached + ".json"))
            {
                using JsonDocument stored = JsonDocument.Parse(File.ReadAllText(cached + ".json"));
                return Reflect(File.ReadAllBytes(cached + ".spv"), stored.RootElement, samplerSpace, uniformSpace);
            }
        }

        CompiledShader compiled = CompileFresh(path, entry, stage, samplerSpace, uniformSpace, bindGlobals, out string reflection);
        if (cached is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cached)!);
            File.WriteAllBytes(cached + ".spv", compiled.Spirv);
            File.WriteAllText(cached + ".json", reflection);
        }

        return compiled;
    }

    private static string CacheKey(string source, string flags)
    {
        _libraryHash ??= LibraryHash();
        string version = typeof(SlangCompiler).Assembly.GetName().Version?.ToString() ?? "";
        byte[] hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{version}\n{_libraryHash}\n{flags}\n{source}"));
        return Convert.ToHexString(hash)[..32];
    }

    /// <summary>One hash over every module in the library, so editing one invalidates everything that may import it.</summary>
    private static string LibraryHash()
    {
        if (LibraryPath is not { } library || !Directory.Exists(library))
            return "";

        var text = new System.Text.StringBuilder();
        foreach (string file in Directory.EnumerateFiles(library, "*.slang", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            text.Append(Path.GetRelativePath(library, file)).Append('\n').Append(File.ReadAllText(file)).Append('\n');
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static CompiledShader CompileFresh(string path, string entry, string stage, int samplerSpace, int uniformSpace, bool bindGlobals, out string reflectionJson)
    {
        Directory.CreateDirectory(ReflectionFolder);
        string json = Path.Combine(ReflectionFolder, Guid.NewGuid().ToString("N") + ".json");
        List<string> args = Arguments(path, entry, stage);
        args.Add("-reflection-json");
        args.Add(json);
        if (bindGlobals)
        {
            // A level shader's global uniforms become its values block: uniform buffer 1.
            args.AddRange(["-fvk-bind-globals", "1", uniformSpace.ToString()]);
        }
        try
        {
            byte[] spirv = SlangCompiler.Compile(args.ToArray());
            reflectionJson = File.ReadAllText(json);
            using JsonDocument reflection = JsonDocument.Parse(reflectionJson);
            return Reflect(spirv, reflection.RootElement, samplerSpace, uniformSpace);
        }
        finally
        {
            File.Delete(json);
        }
    }

    private static List<string> Arguments(string path, string entry, string stage)
    {
        var args = new List<string> { path, "-target", "spirv", "-entry", entry, "-stage", stage };
        if (LibraryPath is { } library)
        {
            args.Add("-I");
            args.Add(library);
        }

        return args;
    }

    private static CompiledShader Reflect(byte[] spirv, JsonElement root, int samplerSpace, int uniformSpace)
    {
        var samplers = new SortedDictionary<int, string>();
        int uniformBuffers = 0;
        ShaderParam[] parameters = [];
        int paramsSize = 0;

        foreach (JsonElement parameter in root.GetProperty("parameters").EnumerateArray())
        {
            if (!parameter.TryGetProperty("binding", out JsonElement binding) ||
                !binding.TryGetProperty("space", out JsonElement spaceElement) ||
                !binding.TryGetProperty("index", out JsonElement indexElement))
                continue;

            int space = spaceElement.GetInt32(), index = indexElement.GetInt32();
            string name = parameter.GetProperty("name").GetString() ?? "";
            JsonElement type = parameter.GetProperty("type");
            string kind = type.GetProperty("kind").GetString() ?? "";

            if (binding.TryGetProperty("kind", out JsonElement bindingKind) && bindingKind.GetString() == "uniform")
                continue;

            if (space == samplerSpace && kind == "resource")
                samplers[index] = name;
            else if (space == uniformSpace && kind == "constantBuffer")
            {
                uniformBuffers = Math.Max(uniformBuffers, index + 1);
                if (index == 1)
                    (parameters, paramsSize) = Fields(type);
            }
        }

        // Global uniforms (a level shader's, bound to buffer 1) are listed one by one.
        List<ShaderParam> globals = new();
        int globalsEnd = 0;
        foreach (JsonElement parameter in root.GetProperty("parameters").EnumerateArray())
        {
            if (parameter.TryGetProperty("binding", out JsonElement binding) &&
                binding.TryGetProperty("kind", out JsonElement bindingKind) && bindingKind.GetString() == "uniform")
            {
                ShaderParam param = Field(parameter);
                globals.Add(param);
                globalsEnd = Math.Max(globalsEnd, param.Offset + param.Size);
            }
        }

        if (globals.Count > 0)
        {
            parameters = [.. parameters, .. globals];
            paramsSize = Math.Max(paramsSize, (globalsEnd + 15) / 16 * 16);
            uniformBuffers = Math.Max(uniformBuffers, 2);
        }

        var names = new string[samplers.Count == 0 ? 0 : samplers.Keys.Max() + 1];
        foreach ((int index, string name) in samplers)
            names[index] = name;

        return new CompiledShader(spirv, names, (uint)uniformBuffers, parameters, paramsSize);
    }

    private static (ShaderParam[] Params, int Size) Fields(JsonElement constantBuffer)
    {
        var list = new List<ShaderParam>();
        JsonElement element = constantBuffer.GetProperty("elementType");
        int end = 0;
        foreach (JsonElement field in element.GetProperty("fields").EnumerateArray())
        {
            ShaderParam param = Field(field);
            list.Add(param);
            end = Math.Max(end, param.Offset + param.Size);
        }

        return (list.ToArray(), (end + 15) / 16 * 16);
    }

    /// <summary>One value's name, place, size, kind and <c>[Default]</c>: a struct field or a global uniform.</summary>
    private static ShaderParam Field(JsonElement field)
    {
        JsonElement binding = field.GetProperty("binding");
        int offset = binding.GetProperty("offset").GetInt32();
        int size = binding.GetProperty("size").GetInt32();
        JsonElement type = field.GetProperty("type");
        string scalar = type.TryGetProperty("scalarType", out JsonElement s) ? s.GetString() ?? ""
            : type.TryGetProperty("elementType", out JsonElement e) && e.TryGetProperty("scalarType", out JsonElement es) ? es.GetString() ?? ""
            : "";

        Vector4 fallback = default;
        if (field.TryGetProperty("userAttribs", out JsonElement attributes))
            foreach (JsonElement attribute in attributes.EnumerateArray())
                if (attribute.GetProperty("name").GetString() == "Default")
                {
                    float[] values = attribute.GetProperty("arguments").EnumerateArray()
                        .Select(a => a.ValueKind == JsonValueKind.Number ? a.GetSingle() : 0f).ToArray();
                    fallback = new Vector4(At(values, 0), At(values, 1), At(values, 2), At(values, 3));
                }

        return new ShaderParam(field.GetProperty("name").GetString() ?? "", offset, size,
            scalar is "int32" or "uint32" or "bool", fallback);
    }

    private static float At(float[] values, int index) => index < values.Length ? values[index] : 0f;
}
