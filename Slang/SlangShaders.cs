using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Slangc.NET;

namespace Xanadu.Slang;

/// <summary>One value an effect reads from its <c>Params</c> block: where it sits, how big, and its default.</summary>
public sealed record ShaderParam(string Name, int Offset, int Size, bool IsInt, Vector4 Default);

/// <summary>
/// A compiled entry point and what reflection says it binds: how many
/// samplers and uniform buffers (for <c>ShaderCreateInfo</c>), the sampler
/// names by binding, and the layout of uniform buffer 1 (an effect's values).
/// </summary>
public sealed class CompiledShader(byte[] spirv, string[] samplers, uint uniformBuffers, ShaderParam[] parameters, int paramsSize, int storageBuffers = 0)
{
    public byte[] Spirv { get; } = spirv;

    /// <summary>Sampler names by binding index in the stage's sampler set; a gap is null.</summary>
    public string[] Samplers { get; } = samplers;

    public uint UniformBuffers { get; } = uniformBuffers;

    public ShaderParam[] Params { get; } = parameters;

    /// <summary>Read-only storage buffers after the samplers: 1 for an effect that keeps state, else 0.</summary>
    public int StorageBuffers { get; } = storageBuffers;

    /// <summary>Bytes in one element of the structured buffer it binds (a kept <c>State</c>); 0 for none.</summary>
    public int StateBytes { get; init; }

    /// <summary>A kept-state effect's once-a-frame <c>update</c>, compiled for compute; null when it keeps none.</summary>
    public CompiledShader? Update { get; init; }

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
public static class SpirvScan
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
public static class SlangShaders
{
    /// <summary>Where <c>import xanadu.*</c> resolves; set by the host once it knows the game folder.</summary>
    public static string? LibraryPath { get; set; }

    /// <summary>The library's named constants, read from its modules (for <c>[Default(color.gold)]</c>).</summary>
    public static ShaderConstants LoadConstants() =>
        LibraryPath is { } library && Directory.Exists(library)
            ? ShaderConstants.FromSources(Directory.EnumerateFiles(library, "*.slang", SearchOption.AllDirectories).Select(File.ReadAllText))
            : new ShaderConstants();

    /// <summary>The library's in-place methods and their copying twins (for chaining <c>c.move(a).rotate(b)</c>).</summary>
    public static ShaderChains LoadChains() =>
        LibraryPath is { } library && Directory.Exists(library)
            ? ShaderChains.FromSources(Directory.EnumerateFiles(library, "*.slang", SearchOption.AllDirectories).Select(File.ReadAllText))
            : new ShaderChains();

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
    /// Compiles a level shader's effect body (Docs/host.md §5.5):
    /// its <c>import</c> lines are hoisted, the rest goes in a namespace of its
    /// own, and a generated entry point runs <c>input(v)</c> then its
    /// <c>shade</c>. <c>#line</c> keeps Slang's errors on the author's lines,
    /// under <paramref name="name"/>.
    /// </summary>
    /// <remarks>
    /// An effect that declares <c>void update(inout State s, float dt)</c> keeps
    /// state (Docs/shaders.md): <c>update</c> runs once a frame on one GPU
    /// thread and <c>shade</c> reads the result as <c>state</c>. Only where
    /// <paramref name="keepsState"/> allows it (the lab, for now); elsewhere such
    /// an effect is an error.
    /// </remarks>
    public static CompiledShader CompileEffectBody(string name, string source, bool keepsState = false)
    {
        bool updates = KeepsState(source);
        if (updates && !keepsState)
            throw new InvalidOperationException($"{name}: update() and kept state work only in the lab for now.");

        int stateBinding = updates ? StateBinding(source) : -1;
        CompiledShader fragment = CompileWrapped(Wrap(name, source, stateBinding), "xanaduFragment", "fragment", 2, 3);
        if (!updates)
            return fragment;

        if (fragment.Samplers.Length != stateBinding)
            throw new InvalidOperationException(
                $"{name}: kept state expected {stateBinding} samplers before it but the shader has {fragment.Samplers.Length}; declare textures as [[vk::binding(n, 2)]] from 2 up, with no gaps.");

        CompiledShader update = CompileWrapped(WrapUpdate(name, source), "xanaduUpdate", "compute", 0, 2);
        if (update.StateBytes > MaxStateBytes)
            throw new InvalidOperationException($"{name}: State takes {update.StateBytes} bytes; it may take at most {MaxStateBytes}.");
        return new CompiledShader(fragment.Spirv, fragment.Samplers, fragment.UniformBuffers, fragment.Params, fragment.ParamsSize, storageBuffers: 1)
        {
            Update = update,
        };
    }

    /// <summary>The most bytes an effect's <c>State</c> may take.</summary>
    public const int MaxStateBytes = 1024;

    /// <summary>Whether an effect body declares an <c>update</c>, and so keeps state.</summary>
    public static bool KeepsState(string source) =>
        System.Text.RegularExpressions.Regex.IsMatch(StripComments(source), @"\bvoid\s+update\s*\(");

    private static string StripComments(string source) =>
        System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(source, @"/\*.*?\*/", " ", System.Text.RegularExpressions.RegexOptions.Singleline),
            @"//[^\n]*", "");

    /// <summary>
    /// Where the state buffer goes in the fragment's set 2: SDL_GPU puts storage
    /// buffers after the samplers, which are the host's two plus the effect's own
    /// <c>[[vk::binding(n, 2)]]</c> textures.
    /// </summary>
    private static int StateBinding(string source)
    {
        int next = 2;
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                     StripComments(source), @"vk::binding\(\s*(\d+)\s*,\s*2\s*\)"))
            next = Math.Max(next, int.Parse(match.Groups[1].Value) + 1);
        return next;
    }

    /// <summary>
    /// Compiles a whole shipped effect from its text, as it would compile from
    /// its file (entry <c>shade</c>), for editing one before it is saved.
    /// Slang's errors name <paramref name="name"/> and the file's own lines.
    /// </summary>
    public static CompiledShader CompileWholeEffect(string name, string source) =>
        CompileWrapped($"#line 1 \"{name.Replace("\"", "'")}\"\n{source}", "shade", "fragment", 2, 3, bindGlobals: false);

    private static CompiledShader CompileWrapped(string code, string entry, string stage, int samplerSpace, int uniformSpace, bool bindGlobals = true)
    {
        Directory.CreateDirectory(ReflectionFolder);
        string file = Path.Combine(ReflectionFolder, Guid.NewGuid().ToString("N") + ".slang");
        File.WriteAllText(file, code);
        try
        {
            return CompileReflected(file, entry, stage, samplerSpace, uniformSpace, bindGlobals);
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>The generated module around one effect body; a chain would add one namespace per effect.</summary>
    /// <param name="stateBinding">The fragment binding for kept state, or -1 when the effect keeps none.</param>
    public static string Wrap(string name, string source, int stateBinding = -1)
    {
        (string imports, string code, string label) = Split(name, source);
        // Read once per pixel, before shade, into a static the body sees as `state`.
        string state = stateBinding < 0 ? "" : $$"""
            [[vk::binding({{stateBinding}}, 2)]] StructuredBuffer<State> xanaduStateIn;
            static State state;
            """;
        string load = stateBinding < 0 ? "" : "effect0::state = effect0::xanaduStateIn[0];";
        // shade gets what its parameters ask for (ShadeSignature): the canvas, the whole input, or both.
        string arguments = ShadeSignature.Arguments(source, out string problem)
                           ?? throw new InvalidOperationException($"{name}: {problem}");
        return $$"""
            import xanadu.stage;
            {{imports}}
            namespace effect0 {
            #line 1 "{{label}}"
            {{code}}
            #line 1 "{{label}} (wrapper)"
            {{state}}
            }
            #line 1 "{{label}} (wrapper)"
            [shader("fragment")]
            float4 xanaduFragment(ItemVaryings v) : SV_Target
            {
                {{load}}
                ItemInput i = input(v);
                i.colour = effect0::shade({{arguments}});
                return i.colour;
            }
            """;
    }

    /// <summary>
    /// The same body with a compute entry that runs its <c>update</c> on the kept
    /// state: buffer 0 of compute set 1, the frame's dt in uniform buffer 0 and
    /// the effect's values (laid out as the fragment's) in uniform buffer 1.
    /// </summary>
    public static string WrapUpdate(string name, string source)
    {
        (string imports, string code, string label) = Split(name, source);
        return $$"""
            import xanadu.stage;
            {{imports}}
            namespace effect0 {
            #line 1 "{{label}}"
            {{code}}
            #line 1 "{{label}} (wrapper)"
            static State state;
            }
            #line 1 "{{label}} (wrapper)"
            struct XanaduUpdateFrame { float dt; float time; float2 pad; };
            [[vk::binding(0, 2)]] ConstantBuffer<XanaduUpdateFrame> xanaduUpdateFrame;
            [[vk::binding(0, 1)]] RWStructuredBuffer<effect0::State> xanaduStateOut;
            [shader("compute")]
            [numthreads(1, 1, 1)]
            void xanaduUpdate()
            {
                time = xanaduUpdateFrame.time;
                effect0::State s = xanaduStateOut[0];
                effect0::update(s, xanaduUpdateFrame.dt);
                xanaduStateOut[0] = s;
            }
            """;
    }

    public static (string Imports, string Code, string Label) Split(string name, string source)
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
        // Slang's attributes take only literals: a library name in a [Default] becomes its numbers.
        string code = ShaderChains.Library.Rewrite(ShaderConstants.Library.ExpandDefaults(body.ToString()));
        return (imports.ToString(), code, label);
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
        int uniformBuffers = 0, stateBytes = 0;
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

            bool buffer = type.TryGetProperty("baseShape", out JsonElement shape) && shape.GetString() == "structuredBuffer";
            if (buffer && type.TryGetProperty("resultType", out JsonElement element) && element.TryGetProperty("sizes", out JsonElement sizes))
                foreach (JsonElement size in sizes.EnumerateArray())
                    stateBytes = Math.Max(stateBytes, size.GetProperty("value").GetInt32());
            if (space == samplerSpace && kind == "resource" && !buffer)
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

        return new CompiledShader(spirv, names, (uint)uniformBuffers, parameters, paramsSize) { StateBytes = stateBytes };
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
