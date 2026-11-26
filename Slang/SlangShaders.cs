using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace Xanadu.Slang;

/// <summary>
/// Slang to SPIR-V at runtime (<see cref="SlangNative"/>), one entry point at a time,
/// with the shipped library (<c>Assets/Shaders/Library</c>: <c>xanadu.stage</c>,
/// <c>xanadu.colour</c>, …) on the import path. The SPIR-V entry is named <c>main</c>.
/// </summary>
public static partial class SlangShaders
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
            SlangCache.Forget();
            lock (ConversionsLock)
            {
                _libraryText = null;
                _globals = null;
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


    private static readonly Lock ModuleLock = new();

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
        if (LibraryPath is not { } library || !Directory.Exists(library))
            return null;
        // Without a cache the prepared library still has to exist: its globals are bound there.
        string cache = CacheFolder ?? Path.Combine(ReflectionFolder, "cache");
        lock (ModuleLock)
        {
            string version = typeof(Slangc.NET.SlangCompiler).Assembly.GetName().Version?.ToString() ?? "";
            string key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                $"{version}\n{SlangCache.LibraryHash}\n{string.Join(';', defines.Order(StringComparer.Ordinal))}")))[..24];
            string folder = Path.Combine(cache, "modules", key);
            if (ModuleFolders.TryGetValue(folder, out string? known))
                return known;

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            string? problem = null;
            string? built = Directory.Exists(folder) ? folder
                : SlangModules.Build(library, folder, defines, out problem, Path.Combine(cache, "modules", "store"), version);
            // Building is Slang's time too: the compile that waited for it is told so.
            _slangTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            ModuleProblem = problem;
            ModuleFolders[folder] = built;
            return built;
        }
    }

    private static string[]? _globals;

    /// <summary>
    /// Every global the library declares (<c>[Global] public StructuredBuffer&lt;T&gt; name;</c>,
    /// Docs/shader-pipeline.md §0), in binding order: modules by path, then as declared. Every
    /// effect stage binds them all, in this order, at <see cref="GlobalDefines"/>' places.
    /// </summary>
    public static IReadOnlyList<string> Globals
    {
        get
        {
            lock (ConversionsLock)
            {
                if (_globals is not null)
                    return _globals;
                var names = new List<string>();
                foreach ((string path, string text) in LibraryModules())
                {
                    string code = StripComments(text);
                    // A marker on anything else would compile with whatever binding Slang picks: wrong data, no error.
                    MatchCollection declared = SlangModules.GlobalDeclaration().Matches(code);
                    foreach (Match marker in Regex.Matches(code, @"\[Global\]"))
                    {
                        if (declared.All(match => match.Index != marker.Index))
                        {
                            int line = code[..marker.Index].Count(c => c == '\n') + 1;
                            throw new InvalidOperationException(
                                $"{Path.GetFileName(path)}({line}): [Global] marks only `public StructuredBuffer<T> name;` (read-only, public, one per line).");
                        }
                    }

                    foreach (Match match in declared)
                    {
                        string name = match.Groups[2].Value;
                        if (names.Contains(name))
                            throw new InvalidOperationException($"{Path.GetFileName(path)}: global {name} is declared twice in the library.");
                        names.Add(name);
                    }
                }

                return _globals = [.. names];
            }
        }
    }

    /// <summary>The defines placing every global from binding <paramref name="first"/> of descriptor set <paramref name="set"/>.</summary>
    internal static string[] GlobalDefines(int first, int set) =>
        [.. Globals.Select((name, i) => $"{SlangModules.GlobalBindingDefine(name)}={first + i}"), $"{SlangModules.GlobalSetDefine}={set}"];

    /// <summary>
    /// Throws when the compiled stage reads a global nothing in this run declares
    /// (<see cref="Xanadu.Core.Rendering.ShaderGlobals"/>): the host it runs in doesn't have that data.
    /// </summary>
    internal static void CheckDeclared(string name, CompiledShader compiled, int set, int first)
    {
        for (int i = 0; i < Globals.Count; i++)
        {
            if (!Core.Rendering.ShaderGlobals.IsDeclared(Globals[i]) && SpirvScan.Binds(compiled.Spirv, set, first + i))
                throw new InvalidOperationException($"{name}: reads the global `{Globals[i]}`, which nothing here declares.");
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
    /// <param name="shadeWith">
    /// A library function of <c>ItemInput</c> drawn instead of the body's <c>shade</c>, through the
    /// same vertex stage (<see cref="TrianglesOverlay"/>, <see cref="UvOverlay"/>); null for <c>shade</c>.
    /// </param>
    public static CompiledShader CompileEffectBody(string name, string source, string? shadeWith = null)
    {
        List<ColourConversion> conversions = [.. ColourConversionsOf(source)];
        string label = Split(name, "").Label;
        for (int pass = 0; ; pass++)
        {
            string compiled = SlangColourConversions.Apply(source, conversions, out int[] origin);
            try
            {
                CompiledShader shader = CompileAsWritten(name, compiled, shadeWith);
                RememberConversions(source, conversions);
                return shader;
            }
            catch (Exception exception) when (pass < MaxConversionPasses)
            {
                // A stack's files each report under their own label: every mark says where its line 1 is.
                List<(string, int)> marks = [(label, 1), .. LineMarks(source)];
                List<ColourConversion> found = [.. SlangColourConversions.Find(exception.Message, marks, source, compiled, origin, LibraryText()).Where(conversion => !conversions.Contains(conversion))];
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

    private static readonly Lock ConversionsLock = new();
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
            KnownConversions[source] = [.. conversions];
        }
    }

    private static string LibraryText()
    {
        lock (ConversionsLock)
            return _libraryText ??= string.Join("\n", LibraryModules().Select(module => module.Text));
    }

    private static CompiledShader CompileAsWritten(string name, string source, string? shadeWith)
    {
        StackBuild? build = BuildOf(name, source = shadeWith is null ? ProbeNodes.Lower(PresentDraws.Lower(source)) : PresentDraws.Lower(source));
        if (shadeWith is not null)
        {
            if (build is not null)
                throw new InvalidOperationException($"{name}: the editor's overlays don't draw graphs yet.");
            // Only the body's deform and values matter: the fragment is the library's.
            int first = StorageBinding(source);
            CompiledShader overlay = CompileWrapped(Wrap(name, source, shadeWith), "xanaduFragment", "fragment", 2, 3, GlobalDefines(first, 2));
            CheckDeclared(name, overlay, 2, first);
            return new CompiledShader(overlay.Spirv, overlay.Samplers, overlay.UniformBuffers, overlay.Params, overlay.ParamsSize, storageBuffers: Globals.Count)
            {
                Vertex = Deforms(source) ? CompileVertex(name, source, null) : null,
            };
        }

        // Storage buffers follow the samplers: every global, then the stack's buffers it reads.
        int globalsAt = StorageBinding(source);
        int own = Globals.Count;
        (CompiledShader fragment, int[] buffers, bool reached) = Placed(build, source, 2, globalsAt + own, (placed, more) => CompileWrapped(
            Wrap(name, placed), "xanaduFragment", "fragment", 2, 3, [.. GlobalDefines(globalsAt, 2), .. more]));
        if (fragment.Samplers.Length != globalsAt)
            throw new InvalidOperationException(
                $"{name}: expected {globalsAt} samplers before the globals but the shader has {fragment.Samplers.Length}; declare textures as [[vk::binding(n, 2)]] from 3 up, with no gaps.");
        CheckDeclared(name, fragment, 2, globalsAt);
        CheckStageBuffers(name, "shade", Bound(own, buffers, reached), build, buffers);
        CheckNoImages(name, "shade", build, fragment, 2);

        return new CompiledShader(fragment.Spirv, fragment.Samplers, fragment.UniformBuffers, fragment.Params, fragment.ParamsSize, Bound(own, buffers, reached))
        {
            Reached = reached,
            ReadsDepth = SpirvScan.Binds(fragment.Spirv, 2, FragmentDepth),
            Vertex = Deforms(source) ? CompileVertex(name, source, build) : null,
            StackBuffers = buffers,
            Build = build is null ? null : CompileBuild(name, source, build, fragment),
            CopiedSource = build is null ? source : null,
        };
    }

    /// <summary>The shader editor's overlay drawing every triangle's edges (line-filled), for <c>shadeWith</c>.</summary>
    public const string TrianglesOverlay = "overlayTriangles";

    /// <summary>The shader editor's overlay marking where uv is 0 and 1 on both axes, for <c>shadeWith</c>.</summary>
    public const string UvOverlay = "overlayUvLines";

    /// <summary>The fragment sampler the depth is (xanadu.stage's <c>depthTexture</c>).</summary>
    public const int FragmentDepth = 2;

    /// <summary>The vertex uniform buffer a deforming effect reads the frame from (0 is the view, 1 its values).</summary>
    public const int VertexFrameSlot = 2;

    /// <summary>
    /// A body's <c>deform</c> as a vertex stage: the globals are the storage buffers of set 0, the
    /// values buffer 1 and the frame buffer <see cref="VertexFrameSlot"/> of set 1. Anything
    /// it binds in the fragment's sets (a texture) is an error: SDL_GPU wouldn't bind it.
    /// </summary>
    private static CompiledShader CompileVertex(string name, string source, StackBuild? build)
    {
        // Vertex set 0: the depth is sampler 0, so the globals' storage buffers start at 1, then the stack's buffers it reads.
        int own = Globals.Count;
        (CompiledShader compiled, int[] buffers, bool reached) = Placed(build, source, 0, 1 + own, (placed, more) => CompileWrapped(
            WrapVertex(name, placed), "xanaduVertex", "vertex", 0, 1,
            [.. GlobalDefines(1, 0), $"XANADU_FRAME_BINDING={VertexFrameSlot}", "XANADU_FRAME_SET=1", "XANADU_DEPTH_BINDING=0", "XANADU_DEPTH_SET=0", .. more]));
        CheckStageBuffers(name, "deform", Bound(own, buffers, reached), build, buffers);
        CheckNoImages(name, "deform", build, compiled, 0);
        if (SpirvScan.BindsSet(compiled.Spirv, 2) || SpirvScan.BindsSet(compiled.Spirv, 3))
            throw new InvalidOperationException(
                $"{name}: deform can't read textures (sampleItem, sampleScreen or the effect's own); only shade can, and both can read sampleDepth.");
        CheckDeclared(name, compiled, 0, 1);
        DeformGrid.Check(name, source, build);
        return new CompiledShader(compiled.Spirv, compiled.Samplers, compiled.UniformBuffers, compiled.Params, compiled.ParamsSize, Bound(own, buffers, reached))
        {
            Reached = reached,
            ReadsDepth = SpirvScan.Binds(compiled.Spirv, 0, 0),
            Grid = DeformGrid.Of(source),
            Translucent = Regex.IsMatch(StripComments(source), @"\[\s*Translucent\s*\]"),
            StackBuffers = buffers,
        };
    }

    /// <summary>Whether an effect body declares <c>ItemVertex deform(…)</c>, and so its own vertex stage.</summary>
    public static bool Deforms(string source) =>
        Regex.IsMatch(StripComments(source), @"\bItemVertex\s+deform\s*\(");

    /// <summary>
    /// Experiments: the game's start-up compile skips the folder, and the shader tool gives their
    /// cards a still instead of a live thumbnail.
    /// </summary>
    public const string LabFolder = "Assets/Shaders/Lab/";

    /// <summary>Whether an effect's path (in the game folder, either slash) is in <see cref="LabFolder"/>.</summary>
    public static bool IsLabOnly(string path) => InFolder(path, LabFolder);

    private static bool InFolder(string path, string folder)
    {
        string slashed = path.Replace('\\', '/');
        return slashed.Contains("/" + folder, StringComparison.Ordinal) || slashed.StartsWith(folder, StringComparison.Ordinal);
    }

    private static string StripComments(string source) =>
        Regex.Replace(
            Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline),
            @"//[^\n]*", "");

    /// <summary>
    /// Where the first storage buffer (the first global) goes in the fragment's set 2:
    /// SDL_GPU puts storage buffers after the samplers, which are the host's three
    /// plus the effect's own <c>[[vk::binding(n, 2)]]</c> textures.
    /// </summary>
    internal static int StorageBinding(string source)
    {
        int next = 3;
        foreach (Match match in Regex.Matches(
                     StripComments(source), @"vk::binding\(\s*(\d+)\s*,\s*2\s*\)"))
            next = Math.Max(next, int.Parse(match.Groups[1].Value) + 1);
        return next;
    }


    internal static CompiledShader CompileWrapped(
        string code, string entry, string stage, int samplerSpace, int uniformSpace, string[]? defines = null, bool bindGlobals = true)
    {
        defines ??= [];
        // Looked up by the code itself: what the cache holds never becomes a file for Slang.
        string? cached = SlangCache.Path(code, entry, stage, samplerSpace, uniformSpace, bindGlobals, defines);
        if (SlangCache.Load(cached, samplerSpace, uniformSpace) is { } known)
            return known;

        Directory.CreateDirectory(ReflectionFolder);
        string file = Path.Combine(ReflectionFolder, Guid.NewGuid().ToString("N") + ".slang");
        File.WriteAllText(file, code);
        try
        {
            CompiledShader compiled = CompileFresh(file, entry, stage, samplerSpace, uniformSpace, bindGlobals, defines, out string reflection);
            if (SpirvScan.Binds(compiled.Spirv, 0, StackBuild.NoSampler))
                throw new InvalidOperationException($"error: {StackBuild.Unsampled}");
            SlangCache.Store(cached, compiled, reflection);
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
        MyRegex().IsMatch(StripComments(source));

    /// <summary>The generated module around one effect body; a chain would add one namespace per effect.</summary>
    /// <param name="shadeWith">A library function of the input drawn instead of the body's shade; see <see cref="CompileEffectBody"/>.</param>
    public static string Wrap(string name, string source, string? shadeWith = null)
    {
        (string imports, string code, string label) = Split(name, source);
        string clock = TimelineClock(source, shading: true);
        // shade gets what its parameters ask for (ShadeSignature): the canvas, the whole input, or both.
        string shade = shadeWith is not null
            ? $"{shadeWith}(i)"
            : "effect0::shade(" + (ShadeSignature.Arguments(source, out string problem)
                                   ?? throw new InvalidOperationException($"{name}: {problem}")) + ")";
        // A deforming effect writes depth: a pixel it leaves clear (a split's seam, a hidden back face)
        // must not, or it would hide the surface behind it.
        string keepOut = Deforms(source) ? "if (i.colour.a <= 0.0) discard;" : "";
        return $$"""
            import xanadu.stage;
            {{imports}}
            namespace effect0 {
            #line 1 "{{label}}"
            {{code}}
            }
            #line 1 "{{label}} (wrapper)"
            [shader("fragment")]
            float4 xanaduFragment(ItemVaryings v, bool front : SV_IsFrontFace) : SV_Target
            {
                copy = v.instance;
                frontFacing = !front;   // the grid winds clockwise on a y-down target, which the default front face calls back
                ItemInput i = input(v);
                {{clock}}
                i.colour = {{shade}};
                {{keepOut}}
                return i.colour;
            }
            """;
    }

    /// <summary>
    /// The same body with a vertex entry that runs its <c>deform</c> on each corner
    /// (as the host's standard stage lays them out), with the globals set from the frame.
    /// </summary>
    public static string WrapVertex(string name, string source)
    {
        (string imports, string code, string label) = Split(name, source);
        string clock = TimelineClock(source, shading: false);
        return $$"""
            import xanadu.stage;
            {{imports}}
            namespace effect0 {
            #line 1 "{{label}}"
            {{code}}
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
            // The host's view (vertex uniform 0): the draw's place in the frame's depth and the
            // slice of it below that the effect's own depths spread over.
            struct XanaduView { float2 screen; float depth; float span; };
            [[vk::binding(0, 1)]] ConstantBuffer<XanaduView> xanaduView;
            [shader("vertex")]
            ItemVaryings xanaduVertex(XanaduCorner c, uint instance : SV_InstanceID)
            {
                vertexClock();
                copy = instance;
                {{clock}}
                ItemVertex v = { c.position, c.uv, c.colour, c.local, canvasFrom(c.restX, c.restY, c.restCorner, c.uv), c.index, 0.0, float3(0.0), float3(0.0) };
                ItemVertex d = effect0::deform(v);
                ItemVaryings o = varyings(d);
                // Depth squashed into 0..1 (0 depth in the middle, nearer is more), then into the slice:
                // nearer corners sit nearer, all of them behind the next draw.
                float near = 0.5 - 0.5 * d.depth / (abs(d.depth) + 1024.0);
                o.position.z = xanaduView.depth - near * xanaduView.span;
                // Where the corner was before deform: shade's rest canvas.
                o.rest = c.position;
                o.instance = instance;
                return o;
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
            return RunSlang([.. Arguments(path, entry, stage, [])]);

        string cached = Path.Combine(folder, SlangCache.Key(source, $"plain|{entry}|{stage}") + ".spv");
        if (File.Exists(cached))
            return File.ReadAllBytes(cached);
        byte[] spirv = RunSlang([.. Arguments(path, entry, stage, [])]);
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
        string? cached = SlangCache.Path(File.ReadAllText(path), entry, stage, samplerSpace, uniformSpace, bindGlobals, defines);
        if (SlangCache.Load(cached, samplerSpace, uniformSpace) is { } known)
            return known;

        CompiledShader compiled = CompileFresh(path, entry, stage, samplerSpace, uniformSpace, bindGlobals, defines, out string reflection);
        SlangCache.Store(cached, compiled, reflection);
        return compiled;
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
            byte[] spirv = RunSlang([.. args]);
            reflectionJson = File.ReadAllText(json);
            using var reflection = JsonDocument.Parse(reflectionJson);
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

        if (LibraryPath is not { } library) return args;

        args.Add("-I");
        args.Add(library);

        return args;
    }

    internal static CompiledShader Reflect(byte[] spirv, JsonElement root, int samplerSpace, int uniformSpace)
    {
        var samplers = new SortedDictionary<int, string>();
        int uniformBuffers = 0;
        var bufferBytes = new Dictionary<string, int>();
        ShaderParam[] parameters = [];
        int paramsSize = 0;

        foreach (JsonElement parameter in root.GetProperty("parameters").EnumerateArray())
        {
            if (!parameter.TryGetProperty("binding", out JsonElement binding) ||
                !binding.TryGetProperty("index", out JsonElement indexElement))
                continue;

            // Slang leaves the space out when it is 0 (a vertex stage's depth sampler).
            int space = binding.TryGetProperty("space", out JsonElement spaceElement) ? spaceElement.GetInt32() : 0;
            int index = indexElement.GetInt32();
            string name = parameter.GetProperty("name").GetString() ?? "";
            JsonElement type = parameter.GetProperty("type");
            string kind = type.GetProperty("kind").GetString() ?? "";

            if (binding.TryGetProperty("kind", out JsonElement bindingKind) && bindingKind.GetString() == "uniform")
                continue;

            bool buffer = type.TryGetProperty("baseShape", out JsonElement shape) && shape.GetString() == "structuredBuffer";
            if (buffer && type.TryGetProperty("resultType", out JsonElement held) && held.TryGetProperty("sizes", out JsonElement heldSizes))
            {
                string bare = name[(name.LastIndexOfAny([':', '.']) + 1)..];
                foreach (JsonElement size in heldSizes.EnumerateArray())
                    bufferBytes[bare] = Math.Max(bufferBytes.GetValueOrDefault(bare), size.GetProperty("value").GetInt32());
            }
            bool image = type.TryGetProperty("access", out JsonElement access) && access.GetString() == "readWrite"; // a graph's image
            if (space == samplerSpace && kind == "resource" && !buffer && !image && index < StackBuild.NoSampler)
                samplers[index] = name;
            else if (space == uniformSpace && kind == "constantBuffer")
            {
                uniformBuffers = Math.Max(uniformBuffers, index + 1);
                if (index == 1)
                    (parameters, paramsSize) = Fields(type);
            }
        }

        // Global uniforms (a level shader's, bound to buffer 1) are listed one by one.
        List<ShaderParam> globals = [];
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

        return new CompiledShader(spirv, names, (uint)uniformBuffers, parameters, paramsSize) { BufferBytes = bufferBytes };
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
        bool fit = false;
        string[]? options = null;
        if (field.TryGetProperty("userAttribs", out JsonElement attributes))
            foreach (JsonElement attribute in attributes.EnumerateArray())
            {
                fit |= attribute.GetProperty("name").GetString() == "Fit";
                if (attribute.GetProperty("name").GetString() == "Options")
                    options = OptionNames(attribute.GetProperty("arguments"));
                if (attribute.GetProperty("name").GetString() == "Default")
                {
                    float[] values = [.. attribute.GetProperty("arguments").EnumerateArray()
                        .Select(a => a.ValueKind switch
                        {
                            JsonValueKind.Number => a.GetSingle(),
                            JsonValueKind.True => 1f,
                            _ => 0f,
                        })];
                    fallback = new Vector4(At(values, 0), At(values, 1), At(values, 2), At(values, 3));
                }
            }

        string kind = type.TryGetProperty("kind", out JsonElement k) ? k.GetString() ?? "" : "";
        int rows = kind == "matrix" && type.TryGetProperty("rowCount", out JsonElement r) ? r.GetInt32() : 1;
        int columns = kind switch
        {
            "matrix" when type.TryGetProperty("columnCount", out JsonElement c) => c.GetInt32(),
            "vector" when type.TryGetProperty("elementCount", out JsonElement c) => c.GetInt32(),
            _ => Math.Clamp(size / 4, 1, 4),
        };

        return new ShaderParam(field.GetProperty("name").GetString() ?? "", offset, size,
            scalar is "int32" or "uint32" or "bool", fallback, scalar == "bool",
            rows, columns, scalar == "uint32") { Fit = fit, Options = options };
    }

    /// <summary>The names an <c>[Options]</c> attribute gives, split by |; null when it gives none.</summary>
    private static string[]? OptionNames(JsonElement arguments)
    {
        foreach (JsonElement argument in arguments.EnumerateArray())
        {
            string? text = argument.ValueKind == JsonValueKind.String ? argument.GetString()
                : argument.ValueKind == JsonValueKind.Object && argument.TryGetProperty("string", out JsonElement s) ? s.GetString()
                : null;
            if (!string.IsNullOrWhiteSpace(text))
                return text.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }
        return null;
    }

    private static float At(float[] values, int index) => index < values.Length ? values[index] : 0f;
    [GeneratedRegex(@"\buniform\s+float2\s+xanaduClock\b")]
    private static partial Regex MyRegex();
}
