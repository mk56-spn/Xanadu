using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Xanadu.Slang;

/// <summary>One value an effect reads from its <c>Params</c> block: where it sits, how big, and its default.</summary>
/// <param name="IsBool">A <c>uniform bool</c>: an on/off switch, held as 0 or 1 (and written as an int, like <paramref name="IsInt"/>).</param>
public sealed record ShaderParam(string Name, int Offset, int Size, bool IsInt, Vector4 Default, bool IsBool = false);

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

    /// <summary>Read-only storage buffers after the samplers: the chart (every effect body), then kept state when it keeps any.</summary>
    public int StorageBuffers { get; } = storageBuffers;

    /// <summary>Bytes in one element of the structured buffer it binds (a kept <c>State</c>); 0 for none.</summary>
    public int StateBytes { get; init; }

    /// <summary>A kept-state effect's once-a-frame <c>update</c>, compiled for compute; null when it keeps none.</summary>
    public CompiledShader? Update { get; init; }

    /// <summary>
    /// An effect's own vertex stage, from its <c>deform</c>; null for the host's standard one.
    /// Its values block is the fragment's, as vertex uniform buffer 1; the frame is buffer 2,
    /// and the chart, when it reads it, the one storage buffer.
    /// </summary>
    public CompiledShader? Vertex { get; init; }

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

    /// <summary>Whether any variable is decorated with this descriptor set, at any binding.</summary>
    public static bool BindsSet(byte[] code, int set) => Bound(code).Any(place => place.Set == set);

    /// <summary>Whether any variable is decorated with this descriptor set and binding.</summary>
    public static bool Binds(byte[] code, int set, int binding) => Bound(code).Contains(((uint)set, (uint)binding));

    /// <summary>Every (set, binding) a variable is decorated with.</summary>
    private static List<(uint Set, uint Binding)> Bound(byte[] code)
    {
        var places = new List<(uint, uint)>();
        if (code.Length < 20)
            return places;

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
            if (sets.TryGetValue(id, out uint s))
                places.Add((s, value));
        return places;
    }
}

/// <summary>
/// Slang to SPIR-V at runtime (<see cref="SlangNative"/>), one entry point at a time,
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

    /// <summary>Every library module's full path and text, in path order; none when the library isn't there.</summary>
    public static List<(string Path, string Text)> LibraryModules() =>
        LibraryPath is { } library && Directory.Exists(library)
            ? Directory.EnumerateFiles(library, "*.slang", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
                .Select(file => (file, File.ReadAllText(file))).ToList()
            : [];

    /// <summary>
    /// Writes one library module (a path from <see cref="LibraryModules"/>), so the next compile
    /// imports it as written; false (and the message) when the disk refuses or the path isn't the library's.
    /// </summary>
    public static bool WriteLibraryModule(string path, string text, out string? error)
    {
        string full = Path.GetFullPath(path);
        if (LibraryPath is not { } library || !full.StartsWith(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(library)) + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
        {
            error = "not a library module";
            return false;
        }

        try
        {
            File.WriteAllText(full, text);
            // The cache key holds the library's hash: it has to be taken again.
            _libraryHash = null;
            lock (ConversionsLock)
            {
                _libraryText = null;
                KnownConversions.Clear();
            }
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// Where compiled shaders are kept between runs (SPIR-V plus reflection),
    /// keyed by a hash of the source, the entry, the flags, every library module
    /// and the compiler's version; null compiles every time.
    /// </summary>
    public static string? CacheFolder { get; set; }

    private static string? _libraryHash;

    private static readonly object ModuleLock = new();

    /// <summary>Each precompiled library by its folder (named by library hash, compiler and defines); null where building it failed.</summary>
    private static readonly Dictionary<string, string?> ModuleFolders = new(StringComparer.Ordinal);

    /// <summary>Why the library couldn't be precompiled last time it was tried; null once it was.</summary>
    public static string? ModuleProblem { get; private set; }

    /// <summary>
    /// The library precompiled for these defines (<see cref="SlangModules"/>), built the first
    /// time it's asked for; null without a cache folder or when Slang refused it, and compiles
    /// read the library's sources as before.
    /// </summary>
    private static string? ModuleFolder(string[] defines)
    {
        if (CacheFolder is not { } cache || LibraryPath is not { } library || !Directory.Exists(library))
            return null;
        lock (ModuleLock)
        {
            _libraryHash ??= LibraryHash();
            string version = typeof(Slangc.NET.SlangCompiler).Assembly.GetName().Version?.ToString() ?? "";
            string key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                $"{version}\n{_libraryHash}\n{string.Join(';', defines.Order(StringComparer.Ordinal))}")))[..24];
            string folder = Path.Combine(cache, "modules", key);
            if (ModuleFolders.TryGetValue(folder, out string? known))
                return known;

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            string? problem = null;
            string? built = Directory.Exists(folder) ? folder
                : SlangModules.Build(library, folder, defines, out problem, Path.Combine(cache, "modules", "store"), version);
            // Building is Slang's time too: the compile that waited for it is told so.
            _slangTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            ModuleProblem = built is null ? problem : null;
            ModuleFolders[folder] = built;
            return built;
        }
    }

    // One shared folder: every file in it has a fresh GUID name and is deleted after use,
    // so launches neither collide nor leave a folder each behind.
    private static readonly string ReflectionFolder =
        Path.Combine(Path.GetTempPath(), "xanadu-slang");

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
    /// <param name="shadeWith">
    /// A library function of <c>ItemInput</c> drawn instead of the body's <c>shade</c>, through the
    /// same vertex stage (<see cref="TrianglesOverlay"/>, <see cref="UvOverlay"/>); null for <c>shade</c>.
    /// The body's own <c>update</c> doesn't run for it.
    /// </param>
    public static CompiledShader CompileEffectBody(string name, string source, bool keepsState = false, string? shadeWith = null)
    {
        List<ColourConversion> conversions = [.. ColourConversionsOf(source)];
        string label = Split(name, "").Label;
        for (int pass = 0; ; pass++)
        {
            string compiled = SlangColourConversions.Apply(source, conversions, out int[] origin);
            try
            {
                CompiledShader shader = CompileAsWritten(name, compiled, keepsState, shadeWith);
                RememberConversions(source, conversions);
                return shader;
            }
            catch (Exception exception) when (pass < MaxConversionPasses)
            {
                List<ColourConversion> found = SlangColourConversions.Find(exception.Message, label, source, compiled, origin, LibraryText())
                    .Where(conversion => !conversions.Contains(conversion)).ToList();
                if (found.Count == 0)
                {
                    RememberConversions(source, conversions);
                    throw;
                }

                conversions.AddRange(found);
            }
        }
    }

    /// <summary>Compiles tried again for float3s found standing as colours: Slang reports most in one go.</summary>
    private const int MaxConversionPasses = 4;

    private static readonly object ConversionsLock = new();
    private static readonly Dictionary<string, ColourConversion[]> KnownConversions = new(StringComparer.Ordinal);
    private static string? _libraryText;

    /// <summary>
    /// The float3s the last compile of exactly this body took as colours (<see cref="SlangColourConversions"/>),
    /// for the editor's hints; empty when it hasn't been compiled.
    /// </summary>
    public static IReadOnlyList<ColourConversion> ColourConversionsOf(string source)
    {
        lock (ConversionsLock)
            return KnownConversions.TryGetValue(source, out ColourConversion[]? known) ? known : [];
    }

    private static void RememberConversions(string source, List<ColourConversion> conversions)
    {
        lock (ConversionsLock)
        {
            // Kept so the next compile of the same body skips the failing pass; the editor makes many.
            if (KnownConversions.Count > 64)
                KnownConversions.Clear();
            KnownConversions[source] = conversions.ToArray();
        }
    }

    private static string LibraryText()
    {
        lock (ConversionsLock)
            return _libraryText ??= string.Join("\n", LibraryModules().Select(module => module.Text));
    }

    private static CompiledShader CompileAsWritten(string name, string source, bool keepsState, string? shadeWith)
    {
        bool updates = KeepsState(source);
        if (updates && !keepsState)
            throw new InvalidOperationException($"{name}: update() and kept state work only in the lab for now (and in {LabFolder}, which the game never loads).");

        if (shadeWith is not null)
        {
            // Only the body's deform and values matter: the fragment is the library's, reading no state.
            CompiledShader overlay = CompileWrapped(
                Wrap(name, source, updates ? StateDeclaredOnly : -1, shadeWith), "xanaduFragment", "fragment", 2, 3, ChartDefines(StorageBinding(source), 2));
            return new CompiledShader(overlay.Spirv, overlay.Samplers, overlay.UniformBuffers, overlay.Params, overlay.ParamsSize, storageBuffers: 0)
            {
                Vertex = Deforms(source) ? CompileVertex(name, source, updates) : null,
            };
        }

        // Storage buffers follow the samplers: the chart, then any kept state.
        int chartBinding = StorageBinding(source);
        int stateBinding = updates ? chartBinding + 1 : -1;
        CompiledShader fragment = CompileWrapped(
            Wrap(name, source, stateBinding), "xanaduFragment", "fragment", 2, 3, ChartDefines(chartBinding, 2));
        if (fragment.Samplers.Length != chartBinding)
            throw new InvalidOperationException(
                $"{name}: expected {chartBinding} samplers before the chart buffer but the shader has {fragment.Samplers.Length}; declare textures as [[vk::binding(n, 2)]] from 2 up, with no gaps.");

        CompiledShader? vertex = Deforms(source) ? CompileVertex(name, source, updates) : null;
        if (!updates)
            return new CompiledShader(fragment.Spirv, fragment.Samplers, fragment.UniformBuffers, fragment.Params, fragment.ParamsSize, storageBuffers: 1)
            {
                Vertex = vertex,
            };

        // Compute: the chart is the read-only buffer of set 0, the state the read-write one of set 1.
        CompiledShader update = CompileWrapped(WrapUpdate(name, source), "xanaduUpdate", "compute", 0, 2, ChartDefines(0, 0));
        if (update.StateBytes > MaxStateBytes)
            throw new InvalidOperationException($"{name}: State takes {update.StateBytes} bytes; it may take at most {MaxStateBytes}.");
        return new CompiledShader(fragment.Spirv, fragment.Samplers, fragment.UniformBuffers, fragment.Params, fragment.ParamsSize, storageBuffers: 2)
        {
            Update = update,
            Vertex = vertex,
        };
    }

    /// <summary>The shader editor's overlay drawing every triangle's edges (line-filled), for <c>shadeWith</c>.</summary>
    public const string TrianglesOverlay = "overlayTriangles";

    /// <summary>The shader editor's overlay marking where uv is 0 and 1 on both axes, for <c>shadeWith</c>.</summary>
    public const string UvOverlay = "overlayUvLines";

    /// <summary>A <see cref="Wrap"/> state binding that declares <c>state</c> (zeros) without reading it.</summary>
    private const int StateDeclaredOnly = -2;

    /// <summary>The vertex uniform buffer a deforming effect reads the frame from (0 is the view, 1 its values).</summary>
    public const int VertexFrameSlot = 2;

    /// <summary>
    /// A body's <c>deform</c> as a vertex stage: the chart is storage buffer 0 of set 0, the
    /// values buffer 1 and the frame buffer <see cref="VertexFrameSlot"/> of set 1. Anything
    /// it binds in the fragment's sets (a texture) is an error: SDL_GPU wouldn't bind it.
    /// </summary>
    private static CompiledShader CompileVertex(string name, string source, bool keepsState)
    {
        CompiledShader compiled = CompileWrapped(WrapVertex(name, source, keepsState), "xanaduVertex", "vertex", 0, 1,
            [.. ChartDefines(0, 0), $"XANADU_FRAME_BINDING={VertexFrameSlot}", "XANADU_FRAME_SET=1"]);
        if (SpirvScan.BindsSet(compiled.Spirv, 2) || SpirvScan.BindsSet(compiled.Spirv, 3))
            throw new InvalidOperationException(
                $"{name}: deform can't read textures (sampleItem, sampleScreen or the effect's own); only shade can.");
        return new CompiledShader(compiled.Spirv, compiled.Samplers, compiled.UniformBuffers, compiled.Params, compiled.ParamsSize,
            storageBuffers: SpirvScan.Binds(compiled.Spirv, set: 0, binding: 0) ? 1 : 0);
    }

    /// <summary>Whether an effect body declares <c>ItemVertex deform(…)</c>, and so its own vertex stage.</summary>
    public static bool Deforms(string source) =>
        System.Text.RegularExpressions.Regex.IsMatch(StripComments(source), @"\bItemVertex\s+deform\s*\(");

    /// <summary>The most bytes an effect's <c>State</c> may take.</summary>
    public const int MaxStateBytes = 16 * 1024;

    /// <summary>Whether an effect body declares an <c>update</c>, and so keeps state.</summary>
    /// <summary>
    /// Effects for the lab and the shader tool only: kept state works in them, and the game never
    /// loads them (its start-up compile skips the folder). Where effects live until levels run state.
    /// </summary>
    public const string LabFolder = "Assets/Shaders/Lab/";

    /// <summary>Whether an effect's path (in the game folder, either slash) is in <see cref="LabFolder"/>.</summary>
    public static bool IsLabOnly(string path) =>
        path.Replace('\\', '/').Contains("/" + LabFolder, StringComparison.Ordinal)
        || path.Replace('\\', '/').StartsWith(LabFolder, StringComparison.Ordinal);

    public static bool KeepsState(string source) =>
        System.Text.RegularExpressions.Regex.IsMatch(StripComments(source), @"\bvoid\s+update\s*\(");

    private static string StripComments(string source) =>
        System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(source, @"/\*.*?\*/", " ", System.Text.RegularExpressions.RegexOptions.Singleline),
            @"//[^\n]*", "");

    /// <summary>
    /// Where the first storage buffer (the chart) goes in the fragment's set 2:
    /// SDL_GPU puts storage buffers after the samplers, which are the host's two
    /// plus the effect's own <c>[[vk::binding(n, 2)]]</c> textures.
    /// </summary>
    private static int StorageBinding(string source)
    {
        int next = 2;
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                     StripComments(source), @"vk::binding\(\s*(\d+)\s*,\s*2\s*\)"))
            next = Math.Max(next, int.Parse(match.Groups[1].Value) + 1);
        return next;
    }

    /// <summary>Where xanadu.stage declares the chart buffer, which moves with each effect's textures.</summary>
    private static string[] ChartDefines(int binding, int set) =>
        [$"XANADU_CHART_BINDING={binding}", $"XANADU_CHART_SET={set}"];

    private static CompiledShader CompileWrapped(
        string code, string entry, string stage, int samplerSpace, int uniformSpace, string[]? defines = null, bool bindGlobals = true)
    {
        defines ??= [];
        // Looked up by the code itself: what the cache holds never becomes a file for Slang.
        string? cached = CachePath(code, entry, stage, samplerSpace, uniformSpace, bindGlobals, defines);
        if (FromCache(cached, samplerSpace, uniformSpace) is { } known)
            return known;

        Directory.CreateDirectory(ReflectionFolder);
        string file = Path.Combine(ReflectionFolder, Guid.NewGuid().ToString("N") + ".slang");
        File.WriteAllText(file, code);
        try
        {
            CompiledShader compiled = CompileFresh(file, entry, stage, samplerSpace, uniformSpace, bindGlobals, defines, out string reflection);
            ToCache(cached, compiled, reflection);
            return compiled;
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// A <c>float2</c> uniform a tool may declare in a body to drive its clock: when
    /// it is there the wrapper sets <c>time</c> from its x and <c>beat</c> from its y,
    /// so a timeline can scrub what the shader sees. The lab's shader editor does.
    /// </summary>
    public const string ClockOverride = "xanaduClock";

    /// <summary>Whether a body declares <see cref="ClockOverride"/>.</summary>
    public static bool DrivesClock(string source) =>
        System.Text.RegularExpressions.Regex.IsMatch(StripComments(source), $@"\buniform\s+float2\s+{ClockOverride}\b");

    /// <summary>The generated module around one effect body; a chain would add one namespace per effect.</summary>
    /// <param name="stateBinding">The fragment binding for kept state, or -1 when the effect keeps none.</param>
    /// <param name="shadeWith">A library function of the input drawn instead of the body's shade; see <see cref="CompileEffectBody"/>.</param>
    public static string Wrap(string name, string source, int stateBinding = -1, string? shadeWith = null)
    {
        (string imports, string code, string label) = Split(name, source);
        string clock = DrivesClock(source)
            ? $"time = effect0::{ClockOverride}.x; beat = effect0::{ClockOverride}.y; i.time = time; shading.time = time;"
            : "";
        // Read once per pixel, before shade, into a static the body sees as `state`.
        string state = stateBinding == StateDeclaredOnly ? "static State state;"
            : stateBinding < 0 ? "" : $$"""
            [[vk::binding({{stateBinding}}, 2)]] StructuredBuffer<State> xanaduStateIn;
            static State state;
            """;
        string load = stateBinding < 0 ? "" : "effect0::state = effect0::xanaduStateIn[0];";
        // shade gets what its parameters ask for (ShadeSignature): the canvas, the whole input, or both.
        string shade = shadeWith is not null
            ? $"{shadeWith}(i)"
            : "effect0::shade(" + (ShadeSignature.Arguments(source, out string problem)
                                   ?? throw new InvalidOperationException($"{name}: {problem}")) + ")";
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
                {{clock}}
                i.colour = {{shade}};
                return i.colour;
            }
            """;
    }

    /// <summary>
    /// The same body with a vertex entry that runs its <c>deform</c> on each corner
    /// (as the host's standard stage lays them out), with the globals set from the frame.
    /// Kept state isn't there: <c>state</c> reads as zeros, so <c>shade</c> still compiles.
    /// </summary>
    public static string WrapVertex(string name, string source, bool keepsState = false)
    {
        (string imports, string code, string label) = Split(name, source);
        string clock = DrivesClock(source) ? $"time = effect0::{ClockOverride}.x; beat = effect0::{ClockOverride}.y;" : "";
        string state = keepsState ? "static State state;" : "";
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
            struct XanaduCorner
            {
                float2 position : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 colour : TEXCOORD2;
                float2 local : TEXCOORD3;
                float2 restCorner : TEXCOORD4;
                float2 restX : TEXCOORD5;
                float2 restY : TEXCOORD6;
                uint index : TEXCOORD7;
            };
            [shader("vertex")]
            ItemVaryings xanaduVertex(XanaduCorner c)
            {
                vertexClock();
                {{clock}}
                ItemVertex v = { c.position, c.uv, c.colour, c.local, canvasFrom(c.restX, c.restY, c.restCorner, c.uv), c.index };
                // Where the corner was before deform: shade's rest canvas.
                ItemVaryings o = varyings(effect0::deform(v));
                o.rest = c.position;
                return o;
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
        string clock = DrivesClock(source) ? $"time = effect0::{ClockOverride}.x; beat = effect0::{ClockOverride}.y;" : "";
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
                {{clock}}
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

    /// <summary>
    /// Just the SPIR-V of one entry of a module on disk (the host's own shaders: particles, the
    /// screen's mips), kept in the shader cache like the rest, so a launch that finds them all
    /// there never starts Slang. A module that <c>#include</c>s another file compiles every time:
    /// the key wouldn't see that file change.
    /// </summary>
    public static byte[] Compile(string path, string entry, string stage)
    {
        string source = File.ReadAllText(path);
        if (CacheFolder is not { } folder || source.Contains("#include", StringComparison.Ordinal))
            return RunSlang(Arguments(path, entry, stage, []).ToArray());

        string cached = Path.Combine(folder, CacheKey(source, $"plain|{entry}|{stage}") + ".spv");
        if (File.Exists(cached))
            return File.ReadAllBytes(cached);
        byte[] spirv = RunSlang(Arguments(path, entry, stage, []).ToArray());
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(cached, spirv);
        return spirv;
    }

    /// <summary>
    /// Starts Slang on another thread if it hasn't started (about 200 ms), for a tool that is
    /// about to compile what no cache holds: the shader editor's first compile then doesn't wait for it.
    /// </summary>
    public static void Prepare() => System.Threading.Tasks.Task.Run(SlangNative.Warm);

    [ThreadStatic] private static long _slangTicks;

    /// <summary>
    /// Time this thread has spent inside Slang itself since <see cref="ResetSlangTime"/>, in ms:
    /// the rest of a compile is our own preparation (wrapping, rewrites, reflection, the cache).
    /// </summary>
    public static double SlangMilliseconds => _slangTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

    public static void ResetSlangTime() => _slangTicks = 0;

    private static byte[] RunSlang(string[] args)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            return SlangNative.Compile(args);
        }
        finally
        {
            _slangTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
        }
    }

    /// <summary>
    /// Compiles and reflects one entry point. <paramref name="samplerSpace"/> and
    /// <paramref name="uniformSpace"/> are the stage's descriptor sets (§3: 2 and 3
    /// for fragment, 0 and 1 for vertex). Throws with Slang's message on an error.
    /// </summary>
    public static CompiledShader CompileReflected(
        string path, string entry, string stage, int samplerSpace, int uniformSpace, bool bindGlobals = false, string[]? defines = null)
    {
        defines ??= [];
        string? cached = CachePath(File.ReadAllText(path), entry, stage, samplerSpace, uniformSpace, bindGlobals, defines);
        if (FromCache(cached, samplerSpace, uniformSpace) is { } known)
            return known;

        CompiledShader compiled = CompileFresh(path, entry, stage, samplerSpace, uniformSpace, bindGlobals, defines, out string reflection);
        ToCache(cached, compiled, reflection);
        return compiled;
    }

    /// <summary>Where one compile is kept (no extension: <c>.spv</c> and <c>.json</c> sit there); null without a cache folder.</summary>
    private static string? CachePath(string source, string entry, string stage, int samplerSpace, int uniformSpace, bool bindGlobals, string[] defines) =>
        CacheFolder is { } folder
            ? Path.Combine(folder, CacheKey(source, $"{entry}|{stage}|{samplerSpace}|{uniformSpace}|{bindGlobals}|{string.Join(';', defines)}"))
            : null;

    private static CompiledShader? FromCache(string? cached, int samplerSpace, int uniformSpace)
    {
        if (cached is null || !File.Exists(cached + ".spv") || !File.Exists(cached + ".json"))
            return null;
        using JsonDocument stored = JsonDocument.Parse(File.ReadAllText(cached + ".json"));
        return Reflect(File.ReadAllBytes(cached + ".spv"), stored.RootElement, samplerSpace, uniformSpace);
    }

    private static void ToCache(string? cached, CompiledShader compiled, string reflection)
    {
        if (cached is null)
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(cached)!);
        File.WriteAllBytes(cached + ".spv", compiled.Spirv);
        File.WriteAllText(cached + ".json", reflection);
    }

    private static string CacheKey(string source, string flags)
    {
        _libraryHash ??= LibraryHash();
        string version = typeof(Slangc.NET.SlangCompiler).Assembly.GetName().Version?.ToString() ?? "";
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

    private static CompiledShader CompileFresh(
        string path, string entry, string stage, int samplerSpace, int uniformSpace, bool bindGlobals, string[] defines, out string reflectionJson)
    {
        Directory.CreateDirectory(ReflectionFolder);
        string json = Path.Combine(ReflectionFolder, Guid.NewGuid().ToString("N") + ".json");
        List<string> args = Arguments(path, entry, stage, defines);
        foreach (string define in defines)
        {
            args.Add("-D");
            args.Add(define);
        }

        args.Add("-reflection-json");
        args.Add(json);
        if (bindGlobals)
        {
            // A level shader's global uniforms become its values block: uniform buffer 1.
            args.AddRange(["-fvk-bind-globals", "1", uniformSpace.ToString()]);
        }
        try
        {
            byte[] spirv = RunSlang(args.ToArray());
            reflectionJson = File.ReadAllText(json);
            using JsonDocument reflection = JsonDocument.Parse(reflectionJson);
            return Reflect(spirv, reflection.RootElement, samplerSpace, uniformSpace);
        }
        finally
        {
            File.Delete(json);
        }
    }

    private static List<string> Arguments(string path, string entry, string stage, string[] defines)
    {
        var args = new List<string> { path, "-target", "spirv", "-entry", entry, "-stage", stage };
        // The precompiled library first: Slang takes the modules there over the sources.
        if (ModuleFolder(defines) is { } modules)
        {
            args.Add("-I");
            args.Add(modules);
        }

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
            if (buffer && name.Contains("xanaduState", StringComparison.Ordinal) &&
                type.TryGetProperty("resultType", out JsonElement element) && element.TryGetProperty("sizes", out JsonElement sizes))
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
                        .Select(a => a.ValueKind switch
                        {
                            JsonValueKind.Number => a.GetSingle(),
                            JsonValueKind.True => 1f,
                            _ => 0f,
                        }).ToArray();
                    fallback = new Vector4(At(values, 0), At(values, 1), At(values, 2), At(values, 3));
                }

        return new ShaderParam(field.GetProperty("name").GetString() ?? "", offset, size,
            scalar is "int32" or "uint32" or "bool", fallback, scalar == "bool");
    }

    private static float At(float[] values, int index) => index < values.Length ? values[index] : 0f;
}
