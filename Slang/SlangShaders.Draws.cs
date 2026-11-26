using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xanadu.Core.Rendering;

namespace Xanadu.Slang;

/// <summary>A graph's draw, compiled: its vertex stage (the grid, moved by its deform) and its fragment stage (its shade).</summary>
public sealed record CompiledDraw(CompiledShader Vertex, CompiledShader Fragment, int Across, int Down)
{
    /// <summary>The graph's images its shade reads (by their place), in the order they bind: read-only, after its samplers.</summary>
    public int[] Reads { get; init; } = [];

    /// <summary>Marked <c>[Translucent]</c> before its <c>[Draw]</c>: its triangles test the depth and never write it (glows).</summary>
    public bool Translucent { get; init; }

    /// <summary>
    /// Its <c>blend:</c>: how it goes onto its targets, over its material's blend when it is an exit; null for the
    /// material's (an exit) or none (an image draw overwrites).
    /// </summary>
    public BlendMode? Blend { get; init; }

    /// <summary>The uint buffer counting its copies on the GPU (its place in the graph's buffers), or -1: its call says how many.</summary>
    public int CopiesBuffer { get; init; } = -1;

    /// <summary>Where in <see cref="CopiesBuffer"/> the count sits, in uints.</summary>
    public long CopiesAt { get; init; }

    /// <summary>Marked <c>[CoversBox]</c>: the host hands it the item's box as its quad, so it covers the box's pixels.</summary>
    public bool CoversBox { get; init; }

    /// <summary>The vertices it draws: two triangles a cell.</summary>
    public uint Vertices => (uint)(Across * Down * 6);
}

/// <summary>
/// A graph's draws (<c>[Draw(across, down, shade: fn)] ItemVertex name(ItemVertex v)</c>): a grid of triangles over the
/// item's quad, made from the vertex index (no vertex buffer), moved by <c>name</c> as a deform moves corners, coloured
/// by <c>fn</c> as shade colours pixels, into the images its call names, one for each output <c>fn</c> returns
/// (<see cref="DrawOutput"/>): its colour, or each field of a struct, such as xanadu.light's <c>IdTexels</c>. They test
/// and write the item's own draw depth, cleared before its first draw, so the nearest surface wins whatever the order.
/// The images are the item's box, so the box's corner reaches the fragment to put its position back on the target.
/// Bindings as a stack's deform and shade have them: vertex set 0 the depth sampler then the globals, set 1 the view at
/// 0, the values at 1 and the frame at 2; fragment set 2 the samplers then the globals, set 3 the frame at 0, the values
/// at 1 and the box at 2; the graph's buffers a stage reads after its globals. <c>copies:</c> draws the grid again per copy,
/// <c>copy</c> its number in both stages.
/// </summary>
public static partial class SlangShaders
{
    /// <summary>A draw fragment's uniform slot for the item's box corner on the target (set 3): its images are the box's size.</summary>
    public const int DrawBoxSlot = 2;

    private static CompiledPass CompileDraw(string name, string source, StackBuild build, int p)
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

        // The vertex stage: the graph's buffers it reads go after the globals in set 0 (the depth sampler is 0).
        CompiledShader Vertex(string placed) => CompileWrapped(WrapDrawVertex(name, placed, pass.Name, across, down),
            "xanaduDrawVertex", "vertex", 0, 1,
            [.. GlobalDefines(1, 0), $"XANADU_FRAME_BINDING={VertexFrameSlot}", "XANADU_FRAME_SET=1", "XANADU_DEPTH_BINDING=0", "XANADU_DEPTH_SET=0"]);
        CompiledShader vertex = Vertex(body);
        CheckNoImages(name, pass.Name, build, vertex, 1);
        int[] vertexBuffers = ReadBuffers(build, vertex);
        if (vertexBuffers.Length > 0)
            vertex = Vertex(build.Rewrite(source, 0, k => Array.IndexOf(vertexBuffers, k) is var at and >= 0 ? 1 + Globals.Count + at : Unplaced + k));
        CheckStageBuffers(name, pass.Name, Globals.Count + vertexBuffers.Length, build, vertexBuffers);
        CheckDeclared(name, vertex, 0, 1);
        if (SpirvScan.BindsSet(vertex.Spirv, 2) || SpirvScan.BindsSet(vertex.Spirv, 3) || SpirvScan.Binds(vertex.Spirv, 0, 0))
            throw new InvalidOperationException($"{Where(name, source, pass.Start)}: error: a draw's {pass.Name} can't read textures or the depth.");

        // The fragment stage: the images the shade reads, by name, found with everything out of sight, then placed after
        // the samplers, read-only storage textures as SDL_GPU binds them in a fragment stage; the globals after them, then
        // the graph's buffers it reads.
        int globalsAt = StorageBinding(source);
        CompiledShader Fragment(string placed, int globals) => CompileWrapped(WrapDrawFragment(name, placed, shade, arguments, outputs, pass.DepthField, pass.Blend),
            "xanaduDrawFragment", "fragment", 2, 3, GlobalDefines(globals, 2));
        CompiledShader fragment = Fragment(body, globalsAt);
        int[] reads = [.. Enumerable.Range(0, build.Images.Count).Where(k => SpirvScan.Binds(fragment.Spirv, 1, StackBuild.UnplacedImage + k))];
        int[] fragmentBuffers = ReadBuffers(build, fragment);
        if (reads.Length > 0 || fragmentBuffers.Length > 0)
        {
            int buffersAt = globalsAt + reads.Length + Globals.Count;
            fragment = Fragment(build.Rewrite(source, 2, k => Array.IndexOf(fragmentBuffers, k) is var at and >= 0 ? buffersAt + at : Unplaced + k,
                k => Array.IndexOf(reads, k) is var at and >= 0 ? globalsAt + at : StackBuild.UnplacedImage + k, imageSet: 2), globalsAt + reads.Length);
            if (SpirvImages.Writes(fragment.Spirv))
                throw new InvalidOperationException($"{Where(name, source, shadeAt.Index)}: error: a draw's {shade} reads images but can't write them; return what it makes as an output.");
            foreach (int target in DrawTargets(build.Schedule, p))
                if (reads.Contains(target))
                    throw new InvalidOperationException(
                        $"{Where(name, source, shadeAt.Index)}: error: {pass.Name} draws into {build.Images[target].Name}, so its {shade} can't read it too.");
        }

        CheckStageBuffers(name, shade, Globals.Count + fragmentBuffers.Length, build, fragmentBuffers);
        CheckDeclared(name, fragment, 2, globalsAt + reads.Length);
        if (SpirvScan.Binds(fragment.Spirv, 2, 1) || SpirvScan.Binds(fragment.Spirv, 2, FragmentDepth))
            throw new InvalidOperationException($"{Where(name, source, shadeAt.Index)}: error: a draw's {shade} can't read the screen or the depth yet.");

        var drawn = new CompiledDraw(
            new CompiledShader(vertex.Spirv, vertex.Samplers, vertex.UniformBuffers, vertex.Params, vertex.ParamsSize,
                storageBuffers: Globals.Count + vertexBuffers.Length) { StackBuffers = vertexBuffers },
            new CompiledShader(fragment.Spirv, fragment.Samplers, fragment.UniformBuffers, fragment.Params, fragment.ParamsSize,
                storageBuffers: Globals.Count + fragmentBuffers.Length) { StackBuffers = fragmentBuffers },
            across, down) { Reads = reads, CopiesBuffer = pass.CopiesBuffer, CopiesAt = pass.CopiesAt, Translucent = pass.Translucent, Blend = pass.Blend, CoversBox = pass.CoversBox };
        return new CompiledPass(Named(build, p), drawn.Fragment, [], 1, []) { Drawn = drawn };
    }

    /// <summary>The graph's buffers a stage compiled from <see cref="Unbound"/> reads, by their place.</summary>
    private static int[] ReadBuffers(StackBuild build, CompiledShader compiled) =>
        [.. Enumerable.Range(0, build.Buffers.Count).Where(k => SpirvScan.Binds(compiled.Spirv, 1, Unplaced + k))];

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

    /// <summary>Output <paramref name="k"/>'s channels in its target: none when it has the target alone, else <c>.yz</c> and so on.</summary>
    private static string Channels(DrawLayout layout, int k, DrawOutput output) =>
        layout.In(layout.Slots[k].Target).Count() == 1 ? "" : "." + DrawLayout.Swizzle(layout.Slots[k].Channel, output.Texel.Channels);

    /// <summary>The grid's corners, from the vertex index, each moved by the draw's deform and put in the graph's depth.</summary>
    private static string WrapDrawVertex(string name, string source, string deform, int across, int down)
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
            // The item's quad on the target: its corner and sides; and the slice of depth it lands in, 0..1 for
            // the item's own draw depth, the item's slice of the canvas depth for a draw onto the frame.
            struct XanaduDrawQuad { float2 corner; float2 xAxis; float2 yAxis; float2 slice; };
            [[vk::binding(0, 1)]] ConstantBuffer<XanaduDrawQuad> xanaduDrawQuad;
            // What a draw hands its shade beyond an item's: the 3D point and facing its deform set.
            struct XanaduDrawVaryings { ItemVaryings item; float3 world : TEXCOORD4; float3 normal : TEXCOORD5; nointerpolation uint triangle : TEXCOORD6; };
            [shader("vertex")]
            XanaduDrawVaryings xanaduDrawVertex(uint index : SV_VertexID, uint instance : SV_InstanceID)
            {
                vertexClock();
                copy = instance;
                {{TimelineClock(source, shading: false)}}
                uint cell = index / 6, k = index % 6;
                float2 corner = float2((0x1A >> k) & 1, (0x34 >> k) & 1);
                float2 uv = (float2(cell % {{across}}, cell / {{across}}) + corner) / float2({{across}}, {{down}});
                XanaduDrawQuad q = xanaduDrawQuad;
                float2 position = q.corner + uv.x * q.xAxis + uv.y * q.yAxis;
                float2 size = float2(length(q.xAxis), length(q.yAxis));
                ItemVertex v = { position, uv, float4(1.0), uv * size, canvasFrom(q.xAxis, q.yAxis, q.corner, uv), index, 0.0, float3(0.0), float3(0.0) };
                ItemVertex d = effect0::{{deform}}(v);
                ItemVaryings o = varyings(d);
                // The deform's depth squashed into 0..1 as the canvas does it, nearer smaller (tested less-or-equal),
                // then into the slice.
                o.position.z = q.slice.x + q.slice.y * (0.5 + 0.5 * d.depth / (abs(d.depth) + 1024.0));
                o.rest = position;
                o.instance = instance;
                XanaduDrawVaryings drawn = { o, d.world, d.normal, index / 3 };
                return drawn;
            }
            """;
    }

    /// <summary>
    /// The draw's shade, one target an output (<see cref="DrawOutput"/>): its colour, discarded where it adds nothing (the
    /// alpha 0, or every channel 0 when it is premultiplied, where a 0 alpha is light added), or
    /// each field of the struct it returns, in order. The input's depth is the pixel's, pixels into the screen; a
    /// <paramref name="depth"/> field puts its own back into the slice as the vertex stage puts a corner's.
    /// </summary>
    private static string WrapDrawFragment(string name, string source, string shade, string arguments, DrawOutput[] outputs, string? depth,
        BlendMode? blend)
    {
        string empty = blend == BlendMode.Premultiplied ? "all(shown == 0.0)" : "shown.a <= 0.0";
        (string imports, string code, string label) = Split(name, source);
        DrawLayout layout = DrawLayout.Of(outputs);
        string fields = string.Join(" ", layout.Targets.Select((type, t) => $"{type} o{t} : SV_Target{t};"))
                        + (depth is null ? "" : " float xanaduDepth : SV_Depth;");
        string written = layout.Packs
            // Packed: each target starts at zero and each output fills its channels.
            ? "let made = effect0::SHADE(ARGUMENTS);\n"
              + string.Join("\n", layout.Targets.Select((type, t) => $"    o.o{t} = ({type})0;"))
              + "\n" + string.Join("\n", outputs.Select((output, k) =>
                  $"    o.o{layout.Slots[k].Target}{Channels(layout, k, output)} = made.{output.Field};"))
              + (depth is null ? "" : $"\n    o.xanaduDepth = xanaduDrawBox.slice.x + xanaduDrawBox.slice.y * (0.5 + 0.5 * made.{depth} / (abs(made.{depth}) + 1024.0));")
            : outputs[0] is { Field: null, Type: not "float4" }
            ? "o.o0 = effect0::SHADE(ARGUMENTS);"
            : outputs[0].Field is null
            ? """
              float4 shown = effect0::SHADE(ARGUMENTS);
                  if (EMPTY)
                      discard;
                  o.o0 = shown;
              """
            : "let made = effect0::SHADE(ARGUMENTS);\n" + string.Join("\n", outputs.Select((output, k) => $"    o.o{k} = made.{output.Field};"))
              + (depth is null ? "" : $"\n    o.xanaduDepth = xanaduDrawBox.slice.x + xanaduDrawBox.slice.y * (0.5 + 0.5 * made.{depth} / (abs(made.{depth}) + 1024.0));");
        written = written.Replace("SHADE", shade).Replace("ARGUMENTS", arguments).Replace("EMPTY", empty);
        return $$"""
            import xanadu.stage;
            {{imports}}
            namespace effect0 {
            #line 1 "{{label}}"
            {{code}}
            #line 1 "{{label}} (wrapper)"
            }
            #line 1 "{{label}} (wrapper)"
            // Where the fragment is: a draw into images renders them the box's size, so its position is the box's
            // and `shift` (the box's corner) puts it back on the target; a draw onto the frame isn't shifted.
            // `origin` is the box's corner, for the texel in the item's images; `corner` and the axes the item's
            // quad at rest, so `i.rest` is the item's whatever uv the triangles carry (a mesh's own).
            struct XanaduDrawBox { float2 shift; float2 origin; float2 slice; float2 corner; float2 xAxis; float2 yAxis; float2 pad; };
            [[vk::binding({{DrawBoxSlot}}, 3)]] ConstantBuffer<XanaduDrawBox> xanaduDrawBox;
            struct XanaduDrawn { {{fields}} };
            struct XanaduDrawVaryings { ItemVaryings item; float3 world : TEXCOORD4; float3 normal : TEXCOORD5; nointerpolation uint triangle : TEXCOORD6; };
            [shader("fragment")]
            XanaduDrawn xanaduDrawFragment(XanaduDrawVaryings drawn, bool front : SV_IsFrontFace)
            {
                ItemVaryings v = drawn.item;
                copy = v.instance;
                drawnTriangle = drawn.triangle;
                frontFacing = !front;   // the grid winds clockwise on a y-down target, which the default front face calls back
                v.position.xy += xanaduDrawBox.shift;
                // The texel under the pixel in the item's images, as a pixel pass has it.
                group.pixel = uint2(max(v.position.xy - xanaduDrawBox.origin, 0.0));
                ItemInput i = input(v);
                // Pixels into the screen, from z: out of the slice, then z = 0.5 + 0.5 d / (|d| + 1024) undone.
                float r = (v.position.z - xanaduDrawBox.slice.x) / max(xanaduDrawBox.slice.y, 1e-9) * 2.0 - 1.0;
                i.depth = r * 1024.0 / max(1.0 - abs(r), 1e-6);
                Canvas quad = canvasFrom(xanaduDrawBox.xAxis, xanaduDrawBox.yAxis, xanaduDrawBox.corner, float2(0.0));
                i.rest = canvasFrom(quad.xAxis, quad.yAxis, quad.corner, quad.uvAt(v.position.xy));
                i.world = drawn.world;
                i.normal = drawn.normal;
                shading = i;
                {{TimelineClock(source, shading: true)}}
                XanaduDrawn o;
                {{written}}
                return o;
            }
            """;
    }
}
