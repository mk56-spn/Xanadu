using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>A graph's draw, compiled: its vertex stage (the grid, moved by its deform) and its fragment stage (its shade).</summary>
public sealed record CompiledDraw(CompiledShader Vertex, CompiledShader Fragment, int Across, int Down)
{
    /// <summary>The graph's images its shade reads (by their place), in the order they bind: read-only, after its samplers.</summary>
    public int[] Reads { get; init; } = [];

    /// <summary>The vertices it draws: two triangles a cell.</summary>
    public uint Vertices => (uint)(Across * Down * 6);
}

/// <summary>
/// A graph's draws (<c>[Draw(across, down, shade: fn)] ItemVertex name(ItemVertex v)</c>): a grid of triangles over the
/// item's quad, made from the vertex index (no vertex buffer), moved by <c>name</c> as a deform moves corners, coloured
/// by <c>fn</c> as shade colours pixels, into the images its call names, one for each output <c>fn</c> returns
/// (<see cref="DrawOutput"/>): its colour, or each field of a struct, such as xanadu.light's <c>SurfaceTexels</c>. They test
/// and write the item's own draw depth, cleared before its first draw, so the nearest surface wins whatever the order.
/// The images are the item's box, so the box's corner reaches the fragment to put its position back on the target.
/// Bindings as a stack's deform and shade have them: vertex set 0 the depth sampler then the globals, set 1 the view at
/// 0, the values at 1 and the frame at 2; fragment set 2 the samplers then the globals, set 3 the frame at 0, the values
/// at 1 and the box at 2. They don't read the graph's buffers or images yet.
/// </summary>
public static partial class SlangShaders
{
    /// <summary>A draw fragment's uniform slot for the item's box corner on the target (set 3): its images are the box's size.</summary>
    public const int DrawBoxSlot = 2;

    private static CompiledPass CompileDraw(string name, string source, StackBuild build, int p, bool keepsState, PoolShape? pool)
    {
        BuildPass pass = build.Passes[p];
        (int across, int down, string shade, DrawOutput[] outputs) = pass.Draw!.Value;
        string code = StripComments(source);
        Match shadeAt = Regex.Match(code, $@"\b\w+\s+{Regex.Escape(shade)}\s*\(([^)]*)\)");
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

        // The images the shade reads, by name: found with every image out of sight, then placed after the samplers,
        // read-only storage textures as SDL_GPU binds them in a fragment stage, and the globals after them.
        int globalsAt = StorageBinding(source);
        CompiledShader Fragment(string placed, int globals) => CompileWrapped(WrapDrawFragment(name, placed, shade, arguments, outputs, keepsState, pool),
            "xanaduDrawFragment", "fragment", 2, 3, GlobalDefines(globals, 2));
        CompiledShader fragment = Fragment(body, globalsAt);
        int[] reads = [.. Enumerable.Range(0, build.Images.Count).Where(k => SpirvScan.Binds(fragment.Spirv, 1, StackBuild.UnplacedImage + k))];
        if (reads.Length > 0)
        {
            fragment = Fragment(build.Rewrite(source, 1, k => Unplaced + k,
                k => Array.IndexOf(reads, k) is var at and >= 0 ? globalsAt + at : StackBuild.UnplacedImage + k, imageSet: 2), globalsAt + reads.Length);
            if (SpirvImages.Writes(fragment.Spirv))
                throw new InvalidOperationException($"{Where(name, source, shadeAt.Index)}: error: a draw's {shade} reads images but can't write them; return what it makes as an output.");
            foreach (int target in DrawTargets(build.Schedule, p))
                if (reads.Contains(target))
                    throw new InvalidOperationException(
                        $"{Where(name, source, shadeAt.Index)}: error: {pass.Name} draws into {build.Images[target].Name}, so its {shade} can't read it too.");
        }

        CheckDeclared(name, fragment, 2, globalsAt + reads.Length);
        CheckUnbound(name, shade, build, fragment);
        if (SpirvScan.Binds(fragment.Spirv, 2, 1) || SpirvScan.Binds(fragment.Spirv, 2, FragmentDepth))
            throw new InvalidOperationException($"{Where(name, source, shadeAt.Index)}: error: a draw's {shade} can't read the screen or the depth yet.");

        var drawn = new CompiledDraw(
            new CompiledShader(vertex.Spirv, vertex.Samplers, vertex.UniformBuffers, vertex.Params, vertex.ParamsSize, storageBuffers: Globals.Count),
            new CompiledShader(fragment.Spirv, fragment.Samplers, fragment.UniformBuffers, fragment.Params, fragment.ParamsSize, storageBuffers: Globals.Count),
            across, down) { Reads = reads };
        return new CompiledPass(Named(build, p), drawn.Fragment, [], 1, []) { Drawn = drawn };
    }

    /// <summary>Every image a call of draw <paramref name="pass"/> draws into, wherever the schedule calls it.</summary>
    private static IEnumerable<int> DrawTargets(IEnumerable<BuildStep> steps, int pass)
    {
        foreach (BuildStep step in steps)
        {
            IEnumerable<int> found = step switch
            {
                BuildDraw draw when draw.Pass == pass => draw.Targets,
                BuildBranch branch => DrawTargets(branch.Yes, pass).Concat(DrawTargets(branch.No, pass)),
                BuildLoop loop => DrawTargets(loop.Body, pass),
                _ => [],
            };
            foreach (int target in found)
                yield return target;
        }
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

    /// <summary>
    /// The draw's shade, one target an output (<see cref="DrawOutput"/>): its colour, discarded where the alpha is 0, or
    /// each field of the struct it returns, in order. The input's depth is the pixel's, pixels into the screen.
    /// </summary>
    private static string WrapDrawFragment(string name, string source, string shade, string arguments, DrawOutput[] outputs, bool keepsState, PoolShape? pool)
    {
        (string imports, string code, string label) = Split(name, source);
        string state = keepsState ? "static State state;" : "";
        string fields = string.Join(" ", outputs.Select((output, k) => $"{output.Type} o{k} : SV_Target{k};"));
        string written = outputs[0].Field is null
            ? """
              float4 shown = effect0::SHADE(ARGUMENTS);
                  if (shown.a <= 0.0)
                      discard;
                  o.o0 = shown;
              """
            : "let made = effect0::SHADE(ARGUMENTS);\n" + string.Join("\n", outputs.Select((output, k) => $"    o.o{k} = made.{output.Field};"));
        written = written.Replace("SHADE", shade).Replace("ARGUMENTS", arguments);
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
            // The item's box on the target: the draw renders into images its size, so a fragment's position is
            // the box's, and the box's corner puts it back on the target before its input is built.
            struct XanaduDrawBox { float2 origin; float2 pad; };
            [[vk::binding({{DrawBoxSlot}}, 3)]] ConstantBuffer<XanaduDrawBox> xanaduDrawBox;
            struct XanaduDrawn { {{fields}} };
            [shader("fragment")]
            XanaduDrawn xanaduDrawFragment(ItemVaryings v)
            {
                // The texel under the pixel in the item's images, as a pixel pass has it.
                group.pixel = uint2(v.position.xy);
                v.position.xy += xanaduDrawBox.origin;
                ItemInput i = input(v);
                // Pixels into the screen, from the squashed z: z = 0.5 + 0.5 d / (|d| + 1024), undone.
                float r = v.position.z * 2.0 - 1.0;
                i.depth = r * 1024.0 / max(1.0 - abs(r), 1e-6);
                shading = i;
                {{TimelineClock(source, shading: true)}}
                XanaduDrawn o;
                {{written}}
                return o;
            }
            """;
    }
}
