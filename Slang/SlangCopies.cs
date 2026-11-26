using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>A stack's copied form: its own vertex and fragment entries, drawing many copies of a mesh in one draw.</summary>
/// <param name="ValuesSize">Bytes one copy's values take, laid out as the stack's values block.</param>
public sealed record CompiledCopies(CompiledShader Vertex, CompiledShader Fragment, int ValuesSize);

/// <summary>
/// The copied form of a stack (Docs/xif-modules.md, "Drawing on the render graph"): the same body drawn once per copy
/// of a mesh kept in its own space, every copy with its own place, tint, unit and values, all in one instanced draw.
/// Nothing in the body changes and nothing is marked: each <c>uniform</c> becomes a per-copy variable that the entries
/// load before running <c>deform</c> and <c>shade</c>, from bytes laid out exactly as the values block is, so the host
/// packs a copy's values as it packs a run's.
/// </summary>
/// <remarks>
/// Bindings, after the globals in each stage: <c>xanaduCopies</c> (one <c>XanaduCopy</c> per copy) then
/// <c>xanaduCopyValues</c> (every copy's values, each at its <c>valuesAt</c>). Vertex uniform 0 is the view, with the
/// draw's first copy; the frame stays at <see cref="SlangShaders.VertexFrameSlot"/>. <c>copy</c> is the copy's place
/// in the frame's list.
/// </remarks>
public static partial class SlangCopies
{
    /// <summary>Bytes in one <c>XanaduCopy</c>: origin, x and y axes, unit, uv shift, modulate, flash, values offset.</summary>
    public const int CopyBytes = 80;

    /// <summary>Why a stack has no copied form; null when it has one.</summary>
    public static string? Refusal(CompiledShader effect) =>
        effect.CopiedSource is null ? "a graph draws its own copies"
        : effect.StackBuffers.Length > 0 || effect.Vertex?.StackBuffers.Length > 0 ? "it reads a stack's buffers"
        : effect.Vertex?.Grid is { } grid && grid.Columns * grid.Rows > 1 ? "its deform cuts a [Grid]"
        : null;

    /// <summary>Compiles the copied form of <paramref name="effect"/>, a stack compiled as <paramref name="name"/>.</summary>
    public static CompiledCopies Compile(string name, CompiledShader effect)
    {
        if (Refusal(effect) is { } why)
            throw new InvalidOperationException($"{name}: has no copied form: {why}.");

        string source = effect.CopiedSource!;
        (string imports, string code, string label) = SlangShaders.Split(name, source);
        code = Unshared(code, effect.Params);
        string loads = Loads(effect.Params);
        bool deforms = SlangShaders.Deforms(source);
        int own = SlangShaders.Globals.Count;

        int globalsAt = SlangShaders.StorageBinding(source);
        CompiledShader fragment = SlangShaders.CompileWrapped(
            Fragment(imports, code, label, source, loads, deforms, globalsAt + own), "xanaduFragment", "fragment", 2, 3,
            SlangShaders.GlobalDefines(globalsAt, 2));
        SlangShaders.CheckDeclared(name, fragment, 2, globalsAt);

        CompiledShader vertex = SlangShaders.CompileWrapped(
            Vertex(imports, code, label, source, loads, deforms, 1 + own), "xanaduVertex", "vertex", 0, 1,
            [.. SlangShaders.GlobalDefines(1, 0), $"XANADU_FRAME_BINDING={SlangShaders.VertexFrameSlot}", "XANADU_FRAME_SET=1",
                "XANADU_DEPTH_BINDING=0", "XANADU_DEPTH_SET=0"]);
        SlangShaders.CheckDeclared(name, vertex, 0, 1);

        return new CompiledCopies(
            // Reflection lists the values' ByteAddressBuffer by its binding with the samplers: only those before the storage buffers are samplers.
            new CompiledShader(vertex.Spirv, vertex.Samplers[..Math.Min(1, vertex.Samplers.Length)], vertex.UniformBuffers, [], 0, storageBuffers: own + 2)
            {
                ReadsDepth = vertex.Samplers.Length > 0 && SpirvScan.Binds(vertex.Spirv, 0, 0),
                Translucent = effect.Vertex?.Translucent == true,
            },
            new CompiledShader(fragment.Spirv, fragment.Samplers[..Math.Min(globalsAt, fragment.Samplers.Length)], fragment.UniformBuffers, [], 0,
                storageBuffers: own + 2)
            {
                ReadsDepth = SpirvScan.Binds(fragment.Spirv, 2, SlangShaders.FragmentDepth),
            },
            effect.ParamsSize);
    }

    private const string Copy = """
        struct XanaduCopy
        {
            float2 origin;
            float2 xAxis;
            float2 yAxis;
            float unit;
            float uvShift;
            float4 modulate;
            float4 flash;       // rgb, and how far towards it (a)
            uint valuesAt;
            uint pad0, pad1, pad2;
        };
        """;

    private static string Fragment(string imports, string code, string label, string source, string loads, bool deforms, int copiesAt)
    {
        string clock = SlangShaders.TimelineClock(source, shading: true);
        string shade = "effect0::shade(" + (ShadeSignature.Arguments(source, out string problem)
                                            ?? throw new InvalidOperationException($"{label}: {problem}")) + ")";
        string keepOut = deforms ? "if (i.colour.a <= 0.0) discard;" : "";
        return $$"""
            import xanadu.stage;
            {{imports}}
            namespace effect0 {
            #line 1 "{{label}}"
            {{code}}
            }
            #line 1 "{{label}} (copies)"
            {{Copy}}
            [[vk::binding({{copiesAt}}, 2)]] StructuredBuffer<XanaduCopy> xanaduCopies;
            [[vk::binding({{copiesAt + 1}}, 2)]] ByteAddressBuffer xanaduCopyValues;
            void xanaduLoad(uint at) { {{loads}} }
            [shader("fragment")]
            float4 xanaduFragment(ItemVaryings v, bool front : SV_IsFrontFace) : SV_Target
            {
                copy = v.instance;
                XanaduCopy k = xanaduCopies[copy];
                xanaduLoad(k.valuesAt);
                frontFacing = !front;
                ItemInput i = input(v);
                unit = max(k.unit, 1e-4);
                i.unit = unit;
                shading.unit = unit;
                {{clock}}
                i.colour = {{shade}};
                {{keepOut}}
                return i.colour;
            }
            """;
    }

    private static string Vertex(string imports, string code, string label, string source, string loads, bool deforms, int copiesAt)
    {
        string clock = SlangShaders.TimelineClock(source, shading: false);
        string placed = deforms ? "effect0::deform(v)" : "v";
        // A deform's depths squash into its slice, as the plain wrapper does; without one the draw sits at its place.
        string depth = deforms
            ? "float near = 0.5 - 0.5 * d.depth / (abs(d.depth) + 1024.0); o.position.z = xanaduView.depth - near * xanaduView.span;"
            : "o.position.z = xanaduView.depth;";
        return $$"""
            import xanadu.stage;
            {{imports}}
            namespace effect0 {
            #line 1 "{{label}}"
            {{code}}
            }
            #line 1 "{{label}} (copies)"
            {{Copy}}
            [[vk::binding({{copiesAt}}, 0)]] StructuredBuffer<XanaduCopy> xanaduCopies;
            [[vk::binding({{copiesAt + 1}}, 0)]] ByteAddressBuffer xanaduCopyValues;
            void xanaduLoad(uint at) { {{loads}} }
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
            // The host's view (vertex uniform 0): the draw's depth and slice, and its first copy in the frame's list.
            struct XanaduView { float2 screen; float depth; float span; uint first; uint pad0, pad1, pad2; };
            [[vk::binding(0, 1)]] ConstantBuffer<XanaduView> xanaduView;
            float2 xanaduPlaced(XanaduCopy k, float2 p) { return k.origin + k.xAxis * p.x + k.yAxis * p.y; }
            float2 xanaduTurned(XanaduCopy k, float2 d) { return k.xAxis * d.x + k.yAxis * d.y; }
            [shader("vertex")]
            ItemVaryings xanaduVertex(XanaduCorner c, uint instance : SV_InstanceID)
            {
                vertexClock();
                uint at = xanaduView.first + instance;
                copy = at;
                XanaduCopy k = xanaduCopies[at];
                xanaduLoad(k.valuesAt);
                unit = max(k.unit, 1e-4);
                {{clock}}
                float2 p = xanaduPlaced(k, c.position);
                float4 tinted = c.colour * k.modulate;
                tinted.rgb = lerp(tinted.rgb, k.flash.rgb, saturate(k.flash.a));
                float2 uv = float2(c.uv.x + k.uvShift, c.uv.y);
                Canvas rest = canvasFrom(xanaduTurned(k, c.restX), xanaduTurned(k, c.restY), xanaduPlaced(k, c.restCorner), c.uv);
                ItemVertex v = { p, uv, tinted, c.local, rest, c.index, 0.0, float3(0.0), float3(0.0) };
                ItemVertex d = {{placed}};
                ItemVaryings o = varyings(d);
                {{depth}}
                o.rest = p;
                o.instance = at;
                return o;
            }
            """;
    }

    [GeneratedRegex(@"(?:\[[^\[\]]*\]\s*)*\buniform\s+([^;{}()]+?)\s+([A-Za-z_]\w*)\s*;")]
    private static partial Regex UniformDeclaration();

    /// <summary>The body with each value's <c>uniform</c> declaration made a plain variable the entries fill per copy.</summary>
    private static string Unshared(string code, ShaderParam[] values)
    {
        var names = new HashSet<string>();
        foreach (ShaderParam value in values)
            names.Add(value.Name);
        return UniformDeclaration().Replace(code, match => names.Contains(match.Groups[2].Value)
            // The attributes go with it; the newlines stay, so the lines Slang reports don't move.
            ? new string('\n', match.Value.Split('\n').Length - 1) + $"static {match.Groups[1].Value} {match.Groups[2].Value};"
            : match.Value);
    }

    /// <summary>Each value read from a copy's bytes at its place in the values block.</summary>
    private static string Loads(ShaderParam[] values)
    {
        var loads = new StringBuilder();
        foreach (ShaderParam value in values)
            loads.Append($"effect0::{value.Name} = {Load(value)}; ");
        return loads.ToString();
    }

    private static string Load(ShaderParam value)
    {
        string at = $"at + {value.Offset}";
        if (value.IsMatrix)
        {
            var rows = new List<string>();
            for (int row = 0; row < value.Rows; row++)
                rows.Add(Words(value with { Rows = 1, Size = value.Columns * 4 }, $"at + {value.Offset + row * 16}"));
            return $"float{value.Rows}x{value.Columns}({string.Join(", ", rows)})";
        }

        return value.IsBool ? $"xanaduCopyValues.Load({at}) != 0" : Words(value, at);
    }

    /// <summary>A scalar or vector's words at <paramref name="at"/>, read as its type.</summary>
    private static string Words(ShaderParam value, string at)
    {
        int count = Math.Clamp(value.Size / 4, 1, 4);
        string load = count == 1 ? $"xanaduCopyValues.Load({at})" : $"xanaduCopyValues.Load{count}({at})";
        return value.IsUnsigned ? load : value.IsInt ? $"asint({load})" : $"asfloat({load})";
    }
}
