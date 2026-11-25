using System.Linq;

namespace Xanadu.Slang;

/// <summary>
/// A graph's pixel passes (<c>[Pixels(w, h)]</c>): compute over the item's pixels on the target, a
/// thread a pixel in tiles of w × h, called with <c>ItemInput</c> and <c>Canvas</c> as <c>trace</c>
/// is. They write the graph's images themselves (<c>colour[group.pixel] = …</c>). Bindings, as every
/// pass has them: the globals and kept state read-only in set 0; the images it touches, then the
/// buffers, read-write in set 1; the frame at uniform 0, the values at 1 and the quad at 2 of set 2.
/// </summary>
public static partial class SlangShaders
{
    /// <summary>
    /// A pass compiled with the graph's images and buffers placed in set 1: the images it touches from 0, then the
    /// buffers it touches, the rest where nothing looks. Returns which of each it binds, in order, and how it uses each buffer.
    /// </summary>
    private static (CompiledShader Shader, int[] Images, int[] Buffers, GraphAccess[] Access) PlacedPass(
        string name, string source, StackBuild build, Func<string, CompiledShader> compile)
    {
        int images = build.Images.Count, buffers = build.Buffers.Count;
        CompiledShader shader = compile(build.Rewrite(source, 1, k => images + k, k => k));
        int[] usedImages = [.. Enumerable.Range(0, images).Where(k => SpirvScan.Binds(shader.Spirv, 1, k))];
        int[] used = [.. Enumerable.Range(0, buffers).Where(k => SpirvScan.Binds(shader.Spirv, 1, images + k))];
        int first = usedImages.Length;
        if (usedImages.Length != images || used.Length != buffers)
            shader = compile(build.Rewrite(source, 1,
                k => Array.IndexOf(used, k) is var at and >= 0 ? first + at : Unplaced + k,
                k => Array.IndexOf(usedImages, k) is var at and >= 0 ? at : StackBuild.UnplacedImage + k));
        int[] coherent = [.. used.Select((k, at) => (k, at)).Where(pair => build.Buffers[pair.k].Coherent).Select(pair => first + pair.at)];
        shader = SpirvCoherence.Applied(shader, 1, coherent);
        if (used.Length > MaxStageBuffers || usedImages.Length > MaxStageBuffers)
            throw new InvalidOperationException(
                $"{name}: a pass touches {usedImages.Length} images and {used.Length} buffers; SDL_GPU binds {MaxStageBuffers} of each a pass.");
        return (shader, usedImages, used, [.. used.Select((_, at) => SpirvScan.Access(shader.Spirv, 1, first + at))]);
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

    /// <summary>A pixel pass compiled: its compute stage, with the images and buffers it binds.</summary>
    private static CompiledPass CompilePixels(string name, string source, StackBuild build, int p, bool keepsState, PoolShape? pool)
    {
        BuildPass pass = build.Passes[p];
        (int width, int height) = pass.Pixels!.Value;
        string arguments = ShadeSignature.Arguments("float4 shade(" + pass.Signature + ")", out string problem)
                           ?? throw new InvalidOperationException($"{Where(name, source, pass.Start)}: error: {problem.Replace("shade", pass.Name)}");
        (CompiledShader compute, int[] images, int[] used, GraphAccess[] access) = PlacedPass(name, source, build, placed =>
            CompileWrapped(WrapPixels(name, placed, pass.Name, arguments, width, height, keepsState, pool), "xanaduPixels", "compute", 0, 2,
                [.. GlobalDefines(0, 0), $"XANADU_DEPTH_BINDING={ComputeDepth}", "XANADU_DEPTH_SET=0", "XANADU_FRAME_BINDING=0", "XANADU_FRAME_SET=2"]));
        CheckDeclared(name, compute, 0, 0);
        if (SpirvScan.Binds(compute.Spirv, 0, ComputeDepth))
            throw new InvalidOperationException($"{Where(name, source, pass.Start)}: error: a pixel pass can't read the canvas depth (sampleDepth) yet.");
        var shader = new CompiledShader(compute.Spirv, compute.Samplers, compute.UniformBuffers, compute.Params, compute.ParamsSize,
            storageBuffers: Globals.Count + (keepsState ? 1 : 0));
        return new CompiledPass(Named(build, p), shader, used, pass.Threads, access) { Images = images, Tile = (width, height) };
    }

    /// <summary>
    /// The body with a compute entry that runs one pixel pass for the pixel under each thread. Every thread of a tile
    /// runs it, even past the box's edge, so barriers in it are met by all; the pass checks <c>group.pixel</c> itself
    /// only when it must (writes past the box land outside the item, on texels nothing shows).
    /// </summary>
    private static string WrapPixels(string name, string source, string pass, string arguments, int width, int height, bool keepsState, PoolShape? pool)
    {
        (string imports, string code, string label) = Split(name, source);
        string state = keepsState
            ? $"[[vk::binding({Globals.Count}, 0)]] StructuredBuffer<State> xanaduStateIn;\nstatic State state;"
            : "";
        string load = keepsState ? "effect0::state = effect0::xanaduStateIn[0];" : "";
        return $$"""
            import xanadu.stage;
            {{imports}}
            namespace effect0 {
            #line 1 "{{label}}"
            {{code}}
            #line 1 "{{label}} (wrapper)"
            {{state}}
            {{ItemDeclaration(pool)}}
            }
            #line 1 "{{label}} (wrapper)"
            struct XanaduTraceQuad { float2 corner; float2 xAxis; float2 yAxis; int2 origin; uint2 size; float dt; float pad; };
            [[vk::binding(2, 2)]] ConstantBuffer<XanaduTraceQuad> xanaduTraceQuad;
            [shader("compute")]
            [numthreads({{width}}, {{height}}, 1)]
            void xanaduPixels(uint3 id : SV_DispatchThreadID, uint3 tile : SV_GroupID, uint3 local : SV_GroupThreadID)
            {
                {{Clocks("frame.time", "frame.beat", "xanaduTraceQuad.dt")}}
                {{load}}
                group.thread = local.xy;
                group.place = tile.xy;
                group.index = local.y * {{width}} + local.x;
                group.threads = {{width * height}};
                group.low = float2(xanaduTraceQuad.origin + int2(tile.xy * uint2({{width}}, {{height}})));
                group.high = group.low + float2({{width}}, {{height}});
                int2 at = xanaduTraceQuad.origin + int2(id.xy);
                group.pixel = uint2(max(at, int2(0)));
                ItemInput i = tracedInput(float2(at) + 0.5, xanaduTraceQuad.corner, xanaduTraceQuad.xAxis, xanaduTraceQuad.yAxis);
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
    private static string TimelineClock(string source, bool shading) => !DrivesClock(source)
        ? ""
        : $"time = effect0::{ClockOverride}.x; beat = effect0::{ClockOverride}.y;" + (shading ? " i.time = time; shading.time = time;" : "");
}
