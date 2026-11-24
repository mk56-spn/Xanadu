using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// The STEP brick (<see cref="BrickKind.Step"/>): <c>[Pool(count)]</c> items of the
/// author's own struct kept on the GPU, stepped once a frame by a compute
/// entry on the <c>ComputeOperation</c> contract (jobs at read-only binding 0,
/// the globals after them, the items at read-write binding 0, the frame at
/// uniform 0 and the values at uniform 1), then drawn once each: the vertex and
/// fragment entries read the one they draw as <c>item</c>, by the instance.
/// </summary>
public static partial class SlangShaders
{
    /// <summary>The most items a pool may keep.</summary>
    public const int MaxPoolItems = 1 << 18;

    /// <summary>Threads in a pool step's workgroup (the <c>ComputeOperation</c>'s).</summary>
    public const int PoolThreads = 64;

    /// <summary>
    /// A pool: its item type's name, how many it keeps, and the births a second when the
    /// engine bears them (<c>[Pool(count, rate)]</c>, with <c>bool step</c> saying when one dies); 0 when
    /// every item lives for good.
    /// </summary>
    public readonly record struct PoolShape(string Type, int Count, float Rate = 0)
    {
        /// <summary>Whether items are born and die: the engine keeps who is alive and bears <see cref="Rate"/> a second.</summary>
        public bool Births => Rate > 0;
    }

    /// <summary>The pool a body keeps (a <c>step</c>), or null; throws when the pool is written wrong.</summary>
    public static PoolShape? PoolOf(string name, string source)
    {
        string code = StripComments(source);
        Match declared = Regex.Match(code, @"\b(?:void|bool)\s+step\s*\(");
        if (!declared.Success)
            return null;

        Match step = Regex.Match(code, @"\b(void|bool)\s+step\s*\(\s*inout\s+(\w+)\s+\w+\s*,\s*uint\s+\w+\s*,\s*float\s+\w+\s*\)");
        if (!step.Success)
            throw new InvalidOperationException(
                $"{Where(name, code, declared.Index)}: error: write step as void step(inout Item t, uint i, float dt) (or bool step, with a rate), with your own struct for Item.");

        // [Pool(count)] or [Pool(count, rate)], on step itself.
        Match named = Regex.Match(code, @"\[\s*Pool\b");
        Match pooled = Regex.Match(code, @"\[\s*Pool\s*\(\s*(\d+)\s*(?:,\s*(\d+(?:\.\d*)?|\.\d+)\s*f?\s*)?\)\s*\]\s*(?:void|bool)\s+step\b");
        if (!pooled.Success)
            throw new InvalidOperationException(named.Success
                ? $"{Where(name, code, named.Index)}: error: write the pool as numbers, right on step: [Pool(1000)] void step(...) or [Pool(1000, 2500)] bool step(...)."
                : $"{Where(name, code, declared.Index)}: error: say how many items the pool keeps on step: [Pool(1000)] void step(inout Item t, uint i, float dt).");
        if (!int.TryParse(pooled.Groups[1].Value, out int items) || items < 1 || items > MaxPoolItems)
            throw new InvalidOperationException(
                $"{Where(name, code, pooled.Index)}: error: a pool keeps 1 to {MaxPoolItems} items.");

        // Born at a rate: step says whether each one lives on.
        bool dies = step.Groups[1].Value == "bool";
        bool born = pooled.Groups[2].Success;
        if (born && !dies)
            throw new InvalidOperationException(
                $"{Where(name, code, declared.Index)}: error: items born at a rate must be able to die: write bool step(...) and return false when one does.");
        if (dies && !born)
            throw new InvalidOperationException(
                $"{Where(name, code, declared.Index)}: error: an item that dies is born again at a rate: say how many a second, [Pool(1000, 2500)] bool step(...).");
        float births = born ? float.Parse(pooled.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
        if (born && births <= 0)
            throw new InvalidOperationException($"{Where(name, code, pooled.Index)}: error: the rate is births a second, more than 0.");
        return new PoolShape(step.Groups[2].Value, items, births);
    }

    /// <summary>Whether the body has its own <c>start</c> for a pool's items.</summary>
    private static bool Starts(string source) => Regex.IsMatch(StripComments(source), @"\b\w+\s+start\s*\(\s*uint\s+\w+\s*\)");

    /// <summary>
    /// The declarations every entry of a pooled body needs, inside its namespace: <c>item</c>, read
    /// nowhere but where it is loaded, and the slot the pool keeps it in, with whether it is alive.
    /// </summary>
    private static string ItemDeclaration(PoolShape? pool) =>
        pool is { } shape ? $"static {shape.Type} item;\nstruct XanaduSlot {{ {shape.Type} item; uint alive; }};" : "";

    /// <summary>
    /// The pool's step as a compute entry: thread <c>i</c> of job <c>SV_GroupID.y</c>, the
    /// item made by <c>start</c> on a job's first run (or zeroed without one), stepped after.
    /// Kept state, when the stack has some, is a read-only buffer after the globals.
    /// </summary>
    private static string WrapPool(string name, string source, PoolShape pool, bool keepsState)
    {
        (string imports, string code, string label) = Split(name, source);
        string clock = DrivesClock(source) ? $"time = effect0::{ClockOverride}.x; beat = effect0::{ClockOverride}.y;" : "";
        string state = keepsState
            ? $"[[vk::binding({1 + Globals.Count}, 0)]] StructuredBuffer<State> xanaduStateIn;\nstatic State state;"
            : "";
        string load = keepsState ? "effect0::state = effect0::xanaduStateIn[0];" : "";
        string made = Starts(source) ? "effect0::start(i)" : "{}";
        string born = Starts(source) ? "effect0::start(job.born + ticket)" : "{}";
        // Every item for good: made after each compile, stepped every frame. Born at a rate: each
        // dead slot takes a ticket, and the first `births` are born there (start gets the birth's
        // number, so every birth differs). This frame's tickets count in slot count + parity; the
        // other counter, untouched this frame, is zeroed for the next.
        string body = pool.Births
            ? $$"""
                if (job.reset != 0)
                {
                    effect0::XanaduSlot empty = {};
                    xanaduPool[job.first + i] = empty;
                    if (i == 0)
                    {
                        xanaduPool[job.first + {{pool.Count}}u] = empty;
                        xanaduPool[job.first + {{pool.Count}}u + 1] = empty;
                    }
                    return;
                }
                if (i == 0)
                    xanaduPool[job.first + {{pool.Count}}u + (1 - job.parity)].alive = 0;
                effect0::XanaduSlot slot = xanaduPool[job.first + i];
                if (slot.alive != 0)
                {
                    if (effect0::step(slot.item, i, xanaduPoolFrame.dt))
                    {
                        xanaduPool[job.first + i] = slot;
                        return;
                    }
                    slot.alive = 0;
                }
                if (job.births > 0)
                {
                    uint ticket;
                    InterlockedAdd(xanaduPool[job.first + {{pool.Count}}u + job.parity].alive, 1u, ticket);
                    if (ticket < job.births)
                    {
                        slot.item = {{born}};
                        slot.alive = 1;
                    }
                }
                xanaduPool[job.first + i] = slot;
                """
            : $$"""
                effect0::XanaduSlot slot;
                slot.alive = 1;
                if (job.reset != 0)
                    slot.item = {{made}};
                else
                {
                    slot = xanaduPool[job.first + i];
                    effect0::step(slot.item, i, xanaduPoolFrame.dt);
                }
                xanaduPool[job.first + i] = slot;
                """;
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
            struct XanaduPoolJob { uint first; uint reset; uint births; uint born; uint parity; uint pad0; uint pad1; uint pad2; };
            struct XanaduPoolFrame { float dt; float time; float2 pad; };
            [[vk::binding(0, 0)]] StructuredBuffer<XanaduPoolJob> xanaduPoolJobs;
            [[vk::binding(0, 1)]] RWStructuredBuffer<effect0::XanaduSlot> xanaduPool;
            [[vk::binding(0, 2)]] ConstantBuffer<XanaduPoolFrame> xanaduPoolFrame;
            [shader("compute")]
            [numthreads({{PoolThreads}}, 1, 1)]
            void xanaduPoolStep(uint3 group : SV_GroupID, uint3 thread : SV_GroupThreadID)
            {
                XanaduPoolJob job = xanaduPoolJobs[group.y];
                uint i = group.x * {{PoolThreads}} + thread.x;
                if (i >= {{pool.Count}}u)
                    return;
                time = xanaduPoolFrame.time;
                dt = xanaduPoolFrame.dt;
                {{clock}}
                {{load}}
            {{body}}
            }
            """;
    }

    /// <summary>The pool's step compiled; its item size from reflection, its count from the code.</summary>
    private static CompiledShader CompilePool(string name, string source, PoolShape pool, bool keepsState)
    {
        // Compute set 0: the jobs at 0, the globals from 1, kept state after them.
        CompiledShader compute = CompileWrapped(WrapPool(name, source, pool, keepsState), "xanaduPoolStep", "compute", 0, 2,
            [.. GlobalDefines(1, 0), $"XANADU_DEPTH_BINDING={ComputeDepth}", "XANADU_DEPTH_SET=0"]);
        CheckDeclared(name, compute, 0, 1);
        if (SpirvScan.Binds(compute.Spirv, 0, ComputeDepth))
            throw new InvalidOperationException($"{name}: step can't read the depth (sampleDepth); only shade and deform can.");
        if (compute.ItemBytes <= 0)
            throw new InvalidOperationException($"{name}: the pool's item {pool.Type} has no size Slang reports.");
        return new CompiledShader(compute.Spirv, compute.Samplers, compute.UniformBuffers, compute.Params, compute.ParamsSize,
            storageBuffers: 1 + Globals.Count + (keepsState ? 1 : 0))
        {
            ItemBytes = compute.ItemBytes,
            ItemCount = pool.Count,
            PoolRate = pool.Rate,
        };
    }

    // ---- where a place in a composed body is -------------------------------------------

    /// <summary>
    /// Every <c>#line 1 "label"</c> mark in a body: the label and the 1-based line of the
    /// body the label's line 1 is. A stack's bricks each carry one (<see cref="ShaderStack.Compose"/>).
    /// </summary>
    public static List<(string Label, int Line)> LineMarks(string source)
    {
        var marks = new List<(string, int)>();
        string[] lines = source.ReplaceLineEndings("\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            Match mark = Regex.Match(lines[i], "^\\s*#line\\s+1\\s+\"([^\"]*)\"");
            if (mark.Success)
                marks.Add((mark.Groups[1].Value, i + 2));
        }

        return marks;
    }

    /// <summary>A place in a body (a character index) as Slang would name it: <c>label(line)</c>, under the last mark before it.</summary>
    public static string Where(string name, string source, int index)
    {
        int line = 1 + source.AsSpan(0, Math.Min(index, source.Length)).Count('\n');
        string label = name;
        int first = 1;
        foreach ((string marked, int at) in LineMarks(source))
        {
            if (at > line)
                break;
            (label, first) = (marked, at);
        }

        return $"{label}({line - first + 1})";
    }
}
