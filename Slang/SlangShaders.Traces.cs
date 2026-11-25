using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// A stack's TRACE brick, compiled: its compute stage, the stack buffers it binds (by their place
/// in <see cref="StackBuild.Buffers"/>) and how it really uses each, its tile, and whether it is all the stack draws (no SHADE brick).
/// </summary>
public sealed record CompiledTrace(CompiledShader Shader, int[] Buffers, int Width, int Height, bool Alone, GraphAccess[] Access);

/// <summary>
/// The TRACE brick (<see cref="StackRole.Trace"/>): <c>trace</c> as compute, a thread a pixel over
/// the quad's box on the target, in tiles of <c>[Tile(w, h)]</c>. Every thread of a tile runs
/// <c>trace</c>, even past the box's edge, so barriers in it are met by all; only those inside
/// write. Its bindings: the depth (sampler 0), the globals and any kept state read-only in set 0;
/// the colour and depth it writes (storage textures 0 and 1), then the stack's buffers it touches,
/// read-write in set 1; the frame at uniform 0, the values at 1 and the quad at 2 of set 2.
/// </summary>
public static partial class SlangShaders
{
    /// <summary>The uniform buffers a trace binds: the frame, the values, the quad.</summary>
    public const int TraceUniforms = 3;

    /// <summary>Its tile when <c>trace</c> carries no <c>[Tile]</c>.</summary>
    private const int TileSide = 8;

    /// <summary>The most threads a tile may hold.</summary>
    private const int MaxTileThreads = 1024;

    [GeneratedRegex(@"\b(?:float4|colour)\s+trace\s*\(([^)]*)\)")]
    private static partial Regex TraceEntry();

    [GeneratedRegex(@"\[\s*Tile\s*\(\s*(\d+)\s*,\s*(\d+)\s*\)\s*\]")]
    private static partial Regex TileAttribute();

    /// <summary>Whether a body declares <c>float4 trace(…)</c>, and so a TRACE brick.</summary>
    public static bool Traces(string source) => TraceEntry().IsMatch(StripComments(source));

    private static CompiledTrace CompileTrace(string name, string source, StackBuild? build, bool keepsState, PoolShape? pool)
    {
        string code = StripComments(source);
        Match tile = TileAttribute().Match(source);
        int width = tile.Success ? int.Parse(tile.Groups[1].Value) : TileSide;
        int height = tile.Success ? int.Parse(tile.Groups[2].Value) : TileSide;
        if (width < 1 || height < 1 || width * height > MaxTileThreads)
            throw new InvalidOperationException(
                $"{Where(name, source, tile.Index)}: error: a tile holds 1 to {MaxTileThreads} threads, not {width} × {height}.");

        // The trace takes Canvas and ItemInput as shade does.
        string arguments = ShadeSignature.Arguments("float4 shade(" + TraceEntry().Match(code).Groups[1].Value + ")", out string problem)
                           ?? throw new InvalidOperationException($"{name}: {problem.Replace("shade", "trace")}");

        (CompiledShader compute, int[] used, GraphAccess[] access) = PlacedWithAccess(build, source, 1, 2, placed =>
            CompileWrapped(WrapTrace(name, placed, arguments, width, height, keepsState, pool), "xanaduTrace", "compute", 0, 2,
                [.. GlobalDefines(1, 0), "XANADU_DEPTH_BINDING=0", "XANADU_DEPTH_SET=0", "XANADU_FRAME_BINDING=0", "XANADU_FRAME_SET=2"]));
        CheckDeclared(name, compute, 0, 1);
        CheckNoImages(name, "trace", build, compute, 1);
        if (used.Length > MaxStageBuffers)
            throw new InvalidOperationException(
                $"{name}: trace touches {used.Length} of the stack's buffers; SDL_GPU binds {MaxStageBuffers} a pass. Share a buffer between arrays.");
        var shader = new CompiledShader(compute.Spirv, compute.Samplers, compute.UniformBuffers, compute.Params, compute.ParamsSize,
            storageBuffers: Globals.Count + (keepsState ? 1 : 0))
        {
            ReadsDepth = SpirvScan.Binds(compute.Spirv, 0, 0),
        };
        return new CompiledTrace(shader, used, width, height, source.Contains(ShaderStack.TracedAlone, StringComparison.Ordinal), access);
    }

    /// <summary>
    /// The body with a compute entry that runs <c>trace</c> for the pixel under each thread and
    /// writes what it returns, and <c>traced.depth</c>, into the host's two textures.
    /// </summary>
    private static string WrapTrace(string name, string source, string arguments, int width, int height, bool keepsState, PoolShape? pool)
    {
        (string imports, string code, string label) = Split(name, source);
        string state = keepsState
            ? $"[[vk::binding({1 + Globals.Count}, 0)]] StructuredBuffer<State> xanaduStateIn;\nstatic State state;"
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
            // The quad on the target (its corner and sides, as a rest canvas has them) and the box of pixels traced.
            struct XanaduTraceQuad { float2 corner; float2 xAxis; float2 yAxis; int2 origin; uint2 size; float dt; float pad; };
            [[vk::binding(2, 2)]] ConstantBuffer<XanaduTraceQuad> xanaduTraceQuad;
            [[vk::binding(0, 1)]] [[vk::image_format("rgba16f")]] RWTexture2D<float4> xanaduTracedColour;
            [[vk::binding(1, 1)]] [[vk::image_format("r32f")]] RWTexture2D<float> xanaduTracedDepth;
            [shader("compute")]
            [numthreads({{width}}, {{height}}, 1)]
            void xanaduTrace(uint3 id : SV_DispatchThreadID, uint3 tile : SV_GroupID, uint3 local : SV_GroupThreadID)
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
                group.pixel = id.xy;
                ItemInput i = tracedInput(float2(at) + 0.5, xanaduTraceQuad.corner, xanaduTraceQuad.xAxis, xanaduTraceQuad.yAxis);
                {{TimelineClock(source, shading: true)}}
                traced.depth = 0.0;
                float4 shown = effect0::trace({{arguments}});
                if (all(id.xy < xanaduTraceQuad.size))
                {
                    xanaduTracedColour[id.xy] = shown;
                    xanaduTracedDepth[id.xy] = traced.depth;
                }
            }
            """;
    }
}
