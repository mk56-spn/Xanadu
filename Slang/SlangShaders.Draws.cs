using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>A graph's draw, compiled: its vertex stage (the grid, moved by its deform) and its fragment stage (its shade).</summary>
public sealed record CompiledDraw(CompiledShader Vertex, CompiledShader Fragment, int Across, int Down)
{
    /// <summary>The vertices it draws: two triangles a cell.</summary>
    public uint Vertices => (uint)(Across * Down * 6);
}

/// <summary>
/// A graph's draws (<c>[Draw(across, down, shade: fn)] ItemVertex name(ItemVertex v)</c>): a grid of triangles over the
/// item's quad, made from the vertex index (no vertex buffer), moved by <c>name</c> as a deform moves corners, coloured
/// by <c>fn</c> as shade colours pixels, into the images its call names: the colour, and the depth as pixels into the
/// screen. They test and write the graph's own depth, cleared at the frame's first draw, so the nearest surface wins
/// whatever the order. Bindings as a stack's deform and shade have them: vertex set 0 the depth sampler then the
/// globals, set 1 the view at 0, the values at 1 and the frame at 2; fragment set 2 the samplers then the globals, set 3
/// the frame at 0 and the values at 1. They don't read the graph's buffers or images yet.
/// </summary>
public static partial class SlangShaders
{
    private static CompiledPass CompileDraw(string name, string source, StackBuild build, int p, bool keepsState, PoolShape? pool)
    {
        BuildPass pass = build.Passes[p];
        (int across, int down, string shade) = pass.Draw!.Value;
        string code = StripComments(source);
        Match shadeAt = Regex.Match(code, $@"\b(?:float4|colour)\s+{Regex.Escape(shade)}\s*\(([^)]*)\)");
        if (!shadeAt.Success)
            throw new InvalidOperationException($"{Where(name, source, pass.Start)}: error: {pass.Name} is shaded by {shade}, so declare float4 {shade}(ItemInput i).");
        string arguments = ShadeSignature.Arguments("float4 shade(" + shadeAt.Groups[1].Value + ")", out string problem)
                           ?? throw new InvalidOperationException($"{Where(name, source, shadeAt.Index)}: error: {problem.Replace("shade", shade)}");
        string body = Unbound(build, source);

        CompiledShader vertex = CompileWrapped(WrapDrawVertex(name, body, pass.Name, across, down, keepsState, pool), "xanaduDrawVertex", "vertex", 0, 1,
            [.. GlobalDefines(1, 0), $"XANADU_FRAME_BINDING={VertexFrameSlot}", "XANADU_FRAME_SET=1", "XANADU_DEPTH_BINDING=0", "XANADU_DEPTH_SET=0"]);
        CheckDeclared(name, vertex, 0, 1);
        CheckUnbound(name, pass.Name, build, vertex);
        if (SpirvScan.BindsSet(vertex.Spirv, 2) || SpirvScan.BindsSet(vertex.Spirv, 3) || SpirvScan.Binds(vertex.Spirv, 0, 0))
            throw new InvalidOperationException($"{Where(name, source, pass.Start)}: error: a draw's {pass.Name} can't read textures or the depth.");

        int globalsAt = StorageBinding(source);
        CompiledShader fragment = CompileWrapped(WrapDrawFragment(name, body, shade, arguments, keepsState, pool), "xanaduDrawFragment", "fragment", 2, 3,
            GlobalDefines(globalsAt, 2));
        CheckDeclared(name, fragment, 2, globalsAt);
        CheckUnbound(name, shade, build, fragment);
        if (SpirvScan.Binds(fragment.Spirv, 2, 1) || SpirvScan.Binds(fragment.Spirv, 2, FragmentDepth))
            throw new InvalidOperationException($"{Where(name, source, shadeAt.Index)}: error: a draw's {shade} can't read the screen or the depth yet.");

        var drawn = new CompiledDraw(
            new CompiledShader(vertex.Spirv, vertex.Samplers, vertex.UniformBuffers, vertex.Params, vertex.ParamsSize, storageBuffers: Globals.Count),
            new CompiledShader(fragment.Spirv, fragment.Samplers, fragment.UniformBuffers, fragment.Params, fragment.ParamsSize, storageBuffers: Globals.Count),
            across, down);
        return new CompiledPass(Named(build, p), drawn.Fragment, [], 1, []) { Drawn = drawn };
    }

    /// <summary>The grid's corners, from the vertex index, each moved by the draw's deform and put in the graph's depth.</summary>
    private static string WrapDrawVertex(string name, string source, string deform, int across, int down, bool keepsState, PoolShape? pool)
    {
        (string imports, string code, string label) = Split(name, source);
        string state = keepsState ? "static State state;" : "";
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
            // The item's quad on the target: its corner and sides.
            struct XanaduDrawQuad { float2 corner; float2 xAxis; float2 yAxis; float2 pad; };
            [[vk::binding(0, 1)]] ConstantBuffer<XanaduDrawQuad> xanaduDrawQuad;
            [shader("vertex")]
            ItemVaryings xanaduDrawVertex(uint index : SV_VertexID)
            {
                vertexClock();
                {{TimelineClock(source, shading: false)}}
                uint cell = index / 6, k = index % 6;
                float2 corner = float2((0x1A >> k) & 1, (0x34 >> k) & 1);
                float2 uv = (float2(cell % {{across}}, cell / {{across}}) + corner) / float2({{across}}, {{down}});
                XanaduDrawQuad q = xanaduDrawQuad;
                float2 position = q.corner + uv.x * q.xAxis + uv.y * q.yAxis;
                float2 size = float2(length(q.xAxis), length(q.yAxis));
                ItemVertex v = { position, uv, float4(1.0), uv * size, canvasFrom(q.xAxis, q.yAxis, q.corner, uv), index, 0.0 };
                ItemVertex d = effect0::{{deform}}(v);
                ItemVaryings o = varyings(d);
                // The deform's depth squashed into 0..1 as the canvas does it, nearer smaller (tested less-or-equal).
                o.position.z = 0.5 + 0.5 * d.depth / (abs(d.depth) + 1024.0);
                o.rest = position;
                o.instance = 0;
                return o;
            }
            """;
    }

    /// <summary>The draw's shade, its colour to target 0 and its depth (pixels into the screen, from the squashed z) to target 1.</summary>
    private static string WrapDrawFragment(string name, string source, string shade, string arguments, bool keepsState, PoolShape? pool)
    {
        (string imports, string code, string label) = Split(name, source);
        string state = keepsState ? "static State state;" : "";
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
            struct XanaduDrawn { float4 colour : SV_Target0; float depth : SV_Target1; };
            [shader("fragment")]
            XanaduDrawn xanaduDrawFragment(ItemVaryings v)
            {
                ItemInput i = input(v);
                {{TimelineClock(source, shading: true)}}
                i.colour = effect0::{{shade}}({{arguments}});
                if (i.colour.a <= 0.0)
                    discard;
                // z = 0.5 + 0.5 d / (|d| + 1024), undone.
                float r = v.position.z * 2.0 - 1.0;
                XanaduDrawn o;
                o.colour = i.colour;
                o.depth = r * 1024.0 / max(1.0 - abs(r), 1e-6);
                return o;
            }
            """;
    }
}
