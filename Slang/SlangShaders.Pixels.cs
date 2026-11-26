using System.Linq;

namespace Xanadu.Slang;

/// <summary>
/// A graph's pixel passes (<c>[Pixels(w, h)]</c>): compute over the item's pixels on the target, a
/// thread a pixel in tiles of w × h, called with <c>ItemInput</c> and <c>Canvas</c> as <c>trace</c>
/// is. They write the item's images themselves at <c>group.pixel</c>, the texel under the thread
/// (<c>colour[group.pixel] = …</c>): images are the item's box, from its top left. With
/// <c>over: name</c> a thread is a texel of that image instead (a scaled or fixed one), and
/// <c>i.pixel</c> is where the texel's centre falls on the box. Bindings, as every
/// pass has them: the globals read-only in set 0; the images it touches, then the
/// buffers, read-write in set 1; the frame at uniform 0, the values at 1 and the quad at 2 of set 2.
/// </summary>
public static partial class SlangShaders
{
    /// <summary>
    /// A pass compiled with the graph's images and buffers placed in set 1: the images it touches from 0, then the
    /// buffers it touches, the rest where nothing looks. Returns which of each it binds, in order, and how it uses each buffer.
    /// </summary>
    private static (CompiledShader Shader, int[] Images, int[] Buffers, GraphAccess[] Access) PlacedPass(
        string name, string source, StackBuild build, Func<string, string[], CompiledShader> compile)
    {
        (CompiledShader shader, int[] images, int[] buffers, GraphAccess[] access, _) = PlacedPass(name, source, build, (placed, _, more) => compile(placed, more), -1);
        return (shader, images, buffers, access);
    }

    /// <summary>
    /// <see cref="PlacedPass(string, string, StackBuild, Func{string, string[], CompiledShader})"/>, and with
    /// <paramref name="samplers"/> at 0 or more, the float4 images its code reads through <c>sampled</c> given samplers in
    /// set 0 from that binding, in order; <paramref name="compile"/> is told how many, so the globals go after them, and
    /// the defines to add. A pass touching any list with <see cref="ListReach.On"/> reaches every list through its pointer
    /// (its shader <see cref="CompiledShader.Reached"/>) and binds only its images.
    /// </summary>
    private static (CompiledShader Shader, int[] Images, int[] Buffers, GraphAccess[] Access, int[] Sampled) PlacedPass(
        string name, string source, StackBuild build, Func<string, int, string[], CompiledShader> compile, int samplers)
    {
        int images = build.Images.Count, buffers = build.Buffers.Count;
        Func<int, int>? unplacedSamplers = samplers < 0 ? null : k => StackBuild.UnplacedSampler + k;
        CompiledShader shader = compile(build.Rewrite(source, 1, k => images + k, k => k, sampled: unplacedSamplers), 0, []);
        int[] usedImages = [.. Enumerable.Range(0, images).Where(k => SpirvScan.Binds(shader.Spirv, 1, k))];
        int[] used = [.. Enumerable.Range(0, buffers).Where(k => SpirvScan.Binds(shader.Spirv, 1, images + k))];
        int[] sampled = samplers < 0 ? [] : [.. Enumerable.Range(0, images).Where(k => SpirvScan.Binds(shader.Spirv, 0, StackBuild.UnplacedSampler + k))];
        foreach (int k in sampled.Intersect(usedImages))
            throw new InvalidOperationException(
                $"{name}: a pass reads {build.Images[k].Name} through sampled() and touches it as an image too; a texture is one or the other within a pass.");
        int first = usedImages.Length;
        Func<int, int> placedImage = k => Array.IndexOf(usedImages, k) is var at and >= 0 ? at : StackBuild.UnplacedImage + k;
        Func<int, int>? placedSampler = unplacedSamplers is null ? null
            : k => Array.IndexOf(sampled, k) is var at and >= 0 ? samplers + at : StackBuild.UnplacedSampler + k;
        if (usedImages.Length > MaxStageBuffers)
            throw new InvalidOperationException($"{name}: a pass touches {usedImages.Length} images; SDL_GPU binds {MaxStageBuffers} a pass.");
        if (Reaches(used))
        {
            GraphAccess[] seen = [.. used.Select(k => SpirvScan.Access(shader.Spirv, 1, images + k))];
            CompiledShader reached = compile(build.Rewrite(source, 1, k => Unplaced + k, placedImage, sampled: placedSampler, reach: 2),
                sampled.Length, [ListReach.Define]);
            return (Reached(build, used, shader, reached), usedImages, used, seen, sampled);
        }

        if (usedImages.Length != images || used.Length != buffers || sampled.Length > 0)
            shader = compile(build.Rewrite(source, 1, k => Array.IndexOf(used, k) is var at and >= 0 ? first + at : Unplaced + k,
                placedImage, sampled: placedSampler), sampled.Length, []);
        int[] coherent = [.. used.Select((k, at) => (k, at)).Where(pair => build.Buffers[pair.k].Coherent).Select(pair => first + pair.at)];
        shader = SpirvCoherence.Applied(shader, 1, coherent);
        if (used.Length > MaxStageBuffers)
            throw new InvalidOperationException(
                $"{name}: a pass touches {used.Length} buffers; SDL_GPU binds {MaxStageBuffers} a pass (without the device layer, which reaches them by pointer).");
        return (shader, usedImages, used, [.. used.Select((_, at) => SpirvScan.Access(shader.Spirv, 1, first + at))], sampled);
    }

    /// <summary>Throws when a stage that isn't a pass touches one of the graph's images: only passes read and write them.</summary>
    private static void CheckNoImages(string name, string stage, StackBuild? build, CompiledShader compiled, int set)
    {
        if (build is null)
            return;
        for (int k = 0; k < build.Images.Count; k++)
            if (SpirvScan.Binds(compiled.Spirv, set, StackBuild.UnplacedImage + k))
                throw new InvalidOperationException(
                    $"{name}: {stage} can't touch the image {build.Images[k].Name}; the graph's passes read and write images, and present shows them.");
    }

    /// <summary>The samplers a pixel pass declares before its globals: the canvas depth, then the screen copy.</summary>
    public const int PixelSamplers = 2;

    /// <summary>
    /// A pixel pass compiled: its compute stage, with the images and buffers it binds. It may read the canvas drawn so
    /// far (<c>sampleScreen</c>, <c>sampleDepth</c>); one that does runs mid-frame, in a break of the item's pass.
    /// </summary>
    private static CompiledPass CompilePixels(string name, string source, StackBuild build, int p)
    {
        BuildPass pass = build.Passes[p];
        (int width, int height) = pass.Pixels!.Value;
        string arguments = ShadeSignature.Arguments("float4 shade(" + pass.Signature + ")", out string problem)
                           ?? throw new InvalidOperationException($"{Where(name, source, pass.Start)}: error: {problem.Replace("shade", pass.Name)}");
        // The canvas so far is compute samplers 0 (its depth) and 1 (the screen copy), always declared; the globals after.
        // The images it reads through sampled() follow them, then the globals.
        (CompiledShader compute, int[] images, int[] used, GraphAccess[] access, int[] sampled) = PlacedPass(name, source, build, (placed, samplers, more) =>
            CompileWrapped(WrapPixels(name, placed, pass.Name, arguments, width, height), "xanaduPixels", "compute", 0, 2,
                [.. GlobalDefines(PixelSamplers + samplers, 0), "XANADU_DEPTH_BINDING=0", "XANADU_DEPTH_SET=0", "XANADU_SCREEN_BINDING=1", "XANADU_SCREEN_SET=0",
                    "XANADU_FRAME_BINDING=0", "XANADU_FRAME_SET=2", .. more]), PixelSamplers);
        CheckDeclared(name, compute, 0, PixelSamplers + sampled.Length);
        var shader = new CompiledShader(compute.Spirv, compute.Samplers, compute.UniformBuffers, compute.Params, compute.ParamsSize,
            storageBuffers: Globals.Count) { Reached = Reaches(used) };
        return new CompiledPass(Named(build, p), shader, used, pass.Threads, access)
        {
            Images = images, ImageAccess = ImageAccess(compute, images), Sampled = sampled, Tile = (width, height), OverImage = pass.OverImage,
            ReadsDepth = SpirvScan.Binds(compute.Spirv, 0, 0), ReadsScreen = SpirvScan.Binds(compute.Spirv, 0, 1),
        };
    }

    /// <summary>
    /// The body with a compute entry that runs one pixel pass for the pixel under each thread. Every thread of a tile
    /// runs it, even past the box's edge, so barriers in it are met by all; the pass checks <c>group.pixel</c> itself
    /// only when it must (writes past the box land outside the item, on texels nothing shows).
    /// </summary>
    private static string WrapPixels(string name, string source, string pass, string arguments, int width, int height)
    {
        (string imports, string code, string label) = Split(name, source);
        return $$"""
            import xanadu.stage;
            {{imports}}
            namespace effect0 {
            #line 1 "{{label}}"
            {{code}}
            #line 1 "{{label}} (wrapper)"
            }
            #line 1 "{{label}} (wrapper)"
            struct XanaduTraceQuad { float2 corner; float2 xAxis; float2 yAxis; int2 origin; uint2 size; float dt; float pad; float2 stretch; float2 pad2; };
            [[vk::binding(2, 2)]] ConstantBuffer<XanaduTraceQuad> xanaduTraceQuad;
            [shader("compute")]
            [numthreads({{width}}, {{height}}, 1)]
            void xanaduPixels(uint3 id : SV_DispatchThreadID, uint3 tile : SV_GroupID, uint3 local : SV_GroupThreadID)
            {
                {{Clocks("frame.time", "frame.beat", "xanaduTraceQuad.dt")}}
                group.thread = local.xy;
                group.place = tile.xy;
                group.index = local.y * {{width}} + local.x;
                group.threads = {{width * height}};
                // Pixels on the target per texel: 1 over the box, more over a smaller image (over: name).
                float2 stretch = xanaduTraceQuad.stretch;
                group.low = float2(xanaduTraceQuad.origin) + float2(tile.xy * uint2({{width}}, {{height}})) * stretch;
                group.high = group.low + float2({{width}}, {{height}}) * stretch;
                group.pixel = id.xy;
                ItemInput i = tracedInput(float2(xanaduTraceQuad.origin) + (float2(id.xy) + 0.5) * stretch,
                    xanaduTraceQuad.corner, xanaduTraceQuad.xAxis, xanaduTraceQuad.yAxis);
                {{TimelineClock(source, shading: true)}}
                effect0::{{pass}}({{arguments}});
            }
            """;
    }

    /// <summary>
    /// Every wrapper's clocks, from where its stage finds them: <c>time</c>, <c>beat</c> and <c>dt</c>, the same
    /// globals whatever the stage, so none can be left out of one.
    /// </summary>
    private static string Clocks(string time, string beat, string dt) =>
        $"time = {time}; beat = {beat}; dt = {dt};";

    /// <summary>The shader tool's timeline, when it drives the clock, over the frame's: set after the input is built.</summary>
    internal static string TimelineClock(string source, bool shading) => !DrivesClock(source)
        ? ""
        : $"time = effect0::{ClockOverride}.x; beat = effect0::{ClockOverride}.y;" + (shading ? " i.time = time; shading.time = time;" : "");
}
