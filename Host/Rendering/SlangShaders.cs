using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
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

    private static readonly string ReflectionFolder =
        Path.Combine(Path.GetTempPath(), $"xanadu-slang-{System.Environment.ProcessId}");

    /// <summary>Just the SPIR-V (the particle spike's use).</summary>
    public static byte[] Compile(string path, string entry, string stage) =>
        SlangCompiler.Compile(Arguments(path, entry, stage).ToArray());

    /// <summary>
    /// Compiles and reflects one entry point. <paramref name="samplerSpace"/> and
    /// <paramref name="uniformSpace"/> are the stage's descriptor sets (§3: 2 and 3
    /// for fragment, 0 and 1 for vertex). Throws with Slang's message on an error.
    /// </summary>
    public static CompiledShader CompileReflected(string path, string entry, string stage, int samplerSpace, int uniformSpace)
    {
        Directory.CreateDirectory(ReflectionFolder);
        string json = Path.Combine(ReflectionFolder, Guid.NewGuid().ToString("N") + ".json");
        List<string> args = Arguments(path, entry, stage);
        args.Add("-reflection-json");
        args.Add(json);
        try
        {
            byte[] spirv = SlangCompiler.Compile(args.ToArray());
            using JsonDocument reflection = JsonDocument.Parse(File.ReadAllText(json));
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

            if (space == samplerSpace && kind == "resource")
                samplers[index] = name;
            else if (space == uniformSpace && kind == "constantBuffer")
            {
                uniformBuffers = Math.Max(uniformBuffers, index + 1);
                if (index == 1)
                    (parameters, paramsSize) = Fields(type);
            }
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

            list.Add(new ShaderParam(
                field.GetProperty("name").GetString() ?? "", offset, size,
                scalar is "int32" or "uint32" or "bool", fallback));
            end = Math.Max(end, offset + size);
        }

        return (list.ToArray(), (end + 15) / 16 * 16);
    }

    private static float At(float[] values, int index) => index < values.Length ? values[index] : 0f;
}
