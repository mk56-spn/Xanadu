using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>A buffer a BUILD brick declares: <c>[Buffer(size)] RWStructuredBuffer&lt;T&gt; name;</c>, kept on the GPU from frame to frame.</summary>
/// <param name="Count">Elements, from the size expression.</param>
/// <param name="Start">Where its declaration starts in the body (the attribute), for the rewrite.</param>
/// <param name="End">Just past its <c>;</c>.</param>
/// <param name="Draws">Marked <c>[Draws]</c> in place of <c>[Buffer(size)]</c>: one uint the passes write, the copies of the
/// stack's quad drawn this frame.</param>
public sealed record BuildBuffer(string Name, string Type, long Count, bool Coherent, int Start, int End, bool Draws = false);

/// <summary>
/// A pass a BUILD brick declares: <c>[Pass(threads, over: n)]</c> (a thread per element of n, the
/// extra threads of the last group turned away) or <c>[Pass(threads, groups: g)]</c> (g whole
/// groups, every thread run: what code with barriers needs), on <c>void name(uint i, uint …)</c>.
/// </summary>
/// <param name="Arguments">The parameters after <c>i</c>: what a schedule's call passes.</param>
/// <param name="Start">Where its <c>[Pass]</c> attribute starts in the body; <paramref name="End"/> is just past it.</param>
public sealed record BuildPass(string Name, int Threads, BuildExpression? Over, BuildExpression? Groups, string[] Arguments,
    int Start, int End);

/// <summary>One step of a <c>build()</c> schedule.</summary>
public abstract record BuildStep(int Index);

/// <summary>A pass run: <c>hist(round);</c>.</summary>
public sealed record BuildCall(int Index, int Pass, BuildExpression[] Arguments) : BuildStep(Index);

/// <summary><c>for (uint v = start; test; update) body</c>; <paramref name="Op"/> is <c>++</c>, <c>--</c> or an assignment like <c>+=</c>.</summary>
public sealed record BuildLoop(int Index, string Variable, BuildExpression Start, BuildExpression Test, string Op, BuildExpression? By,
    BuildStep[] Body) : BuildStep(Index);

/// <summary><c>if (test) yes else no</c>.</summary>
public sealed record BuildBranch(int Index, BuildExpression Test, BuildStep[] Yes, BuildStep[] No) : BuildStep(Index);

/// <summary>A problem in a BUILD brick at a place in the body (a character index), worded for the author.</summary>
public sealed class BuildProblem(int index, string message) : Exception(message)
{
    public int Index { get; } = index;
}

/// <summary>
/// A stack's BUILD brick (<see cref="BrickKind.Build"/>), read: the buffers it keeps, the
/// passes it declares and the <c>build()</c> schedule that orders them. The schedule never
/// runs on the GPU: the CPU walks it every frame (its loops and ifs over constants, uniforms
/// and loop variables) and each call becomes a compute pass of its own, since SDL_GPU orders
/// storage writes between passes. The wrapper places every buffer (<see cref="Rewrite"/>):
/// authors never write a binding.
/// </summary>
public sealed partial class StackBuild
{
    /// <summary>The most passes one frame's schedule may run.</summary>
    public const int MaxCalls = 1024;

    /// <summary>The most arguments a pass takes after <c>i</c>.</summary>
    public const int MaxArguments = 4;

    private const int MaxIterations = 1 << 16;

    private readonly string _code;
    private readonly Dictionary<string, BuildValue?> _constants = [];
    private readonly HashSet<string> _working = [];
    private readonly (int Start, int End) _schedule;

    public IReadOnlyList<BuildBuffer> Buffers { get; }

    /// <summary>The <c>[Draws]</c> buffer's place in <see cref="Buffers"/>, or -1: without one the stack's quad is drawn once.</summary>
    public int Draws { get; }

    public IReadOnlyList<BuildPass> Passes { get; }

    public IReadOnlyList<BuildStep> Schedule { get; }

    private StackBuild(string code, (int, int) schedule)
    {
        _code = code;
        _schedule = schedule;
        List<BuildBuffer> buffers = ReadBuffers();
        Buffers = buffers;
        Draws = buffers.FindIndex(buffer => buffer.Draws);
        Passes = ReadPasses();
        Schedule = ReadSchedule();
    }

    /// <summary>The body's BUILD brick read, or null when it has no <c>build()</c>; throws <see cref="BuildProblem"/> when it is written wrong.</summary>
    public static StackBuild? Of(string source)
    {
        string code = Blank(source);
        Match entry = BuildEntry().Match(code);
        if (!entry.Success)
        {
            Match stray = Regex.Match(code, @"\[\s*(?:(?:Buffer|Pass)\s*\(|Draws\s*\])");
            if (stray.Success)
                throw new BuildProblem(stray.Index, "[Buffer], [Draws] and [Pass] belong in a BUILD brick, with a void build() that runs the passes.");
            return null;
        }

        int open = code.IndexOf('{', entry.Index + entry.Length);
        int close = Matching(code, open);
        if (open < 0 || close < 0)
            throw new BuildProblem(entry.Index, "build() needs a body in braces.");
        return new StackBuild(code, (entry.Index, close + 1));
    }

    [GeneratedRegex(@"\bvoid\s+build\s*\(\s*\)")]
    private static partial Regex BuildEntry();

    [GeneratedRegex(@"\[\s*(?:Buffer\s*\((?<size>[^\]]*)\)|(?<draws>Draws))\s*\]\s*(?<coherent>globallycoherent\s+)?RWStructuredBuffer\s*<\s*(?<type>[\w:]+)\s*>\s*(?<name>\w+)\s*;")]
    private static partial Regex BufferDeclaration();

    [GeneratedRegex(@"(?<attribute>\[\s*Pass\s*\((?<args>[^\]]*)\)\s*\])\s*void\s+(?<name>\w+)\s*\((?<params>[^)]*)\)")]
    private static partial Regex PassDeclaration();

    [GeneratedRegex(@"\bstatic\s+const\s+(?:uint|int|float|bool|half)\s+(?<name>\w+)\s*=\s*(?<value>[^;]+);")]
    private static partial Regex ConstantDeclaration();

    /// <summary>A <c>static const</c> of the body, worked out; null when there is none by that name or it can't be.</summary>
    public BuildValue? Constant(string name)
    {
        if (_constants.TryGetValue(name, out BuildValue? known))
            return known;
        if (!_working.Add(name))
            return null;
        BuildValue? value = null;
        foreach (Match match in ConstantDeclaration().Matches(_code))
        {
            if (match.Groups["name"].Value != name)
                continue;
            try
            {
                value = BuildExpression.Parse(match.Groups["value"].Value).Evaluate(Constant);
            }
            catch (FormatException)
            {
            }

            break;
        }

        _working.Remove(name);
        _constants[name] = value;
        return value;
    }

    private BuildValue Evaluate(BuildExpression expression, int index, Func<string, BuildValue?> lookup)
    {
        try
        {
            return expression.Evaluate(lookup);
        }
        catch (FormatException problem)
        {
            throw new BuildProblem(index, problem.Message + ".");
        }
    }

    private BuildExpression ParseAt(string text, int index)
    {
        try
        {
            return BuildExpression.Parse(text);
        }
        catch (FormatException problem)
        {
            throw new BuildProblem(index, problem.Message + ".");
        }
    }

    private List<BuildBuffer> ReadBuffers()
    {
        var buffers = new List<BuildBuffer>();
        var declared = BufferDeclaration().Matches(_code);
        foreach (Match marker in Regex.Matches(_code, @"\[\s*(?:Buffer\s*\(|Draws\s*\])"))
            if (!declared.Any(match => match.Index == marker.Index))
                throw new BuildProblem(marker.Index, "write a buffer as [Buffer(size)] RWStructuredBuffer<T> name; (globallycoherent may go before RWStructuredBuffer), or [Draws] RWStructuredBuffer<uint> name;.");

        foreach (Match match in declared)
        {
            string name = match.Groups["name"].Value;
            if (buffers.Any(buffer => buffer.Name == name))
                throw new BuildProblem(match.Index, $"the buffer {name} is declared twice.");
            if (match.Groups["draws"].Success)
            {
                if (match.Groups["type"].Value != "uint")
                    throw new BuildProblem(match.Index, $"a [Draws] buffer is one uint, the copies drawn: [Draws] RWStructuredBuffer<uint> {name};");
                if (buffers.Any(buffer => buffer.Draws))
                    throw new BuildProblem(match.Index, "a stack has one [Draws] buffer.");
                buffers.Add(new BuildBuffer(name, "uint", 1, match.Groups["coherent"].Success, match.Index, match.Index + match.Length, Draws: true));
                continue;
            }

            Group size = match.Groups["size"];
            BuildValue count = Evaluate(ParseAt(size.Value, size.Index), size.Index, Constant);
            if (!count.Whole || count.Long < 1)
                throw new BuildProblem(size.Index, $"a buffer's size is a whole number of elements, at least 1 ({name} has {count}).");
            buffers.Add(new BuildBuffer(name, match.Groups["type"].Value, count.Long, match.Groups["coherent"].Success,
                match.Index, match.Index + match.Length));
        }

        return buffers;
    }

    private List<BuildPass> ReadPasses()
    {
        var passes = new List<BuildPass>();
        var declared = PassDeclaration().Matches(_code);
        foreach (Match marker in Regex.Matches(_code, @"\[\s*Pass\s*\("))
            if (!declared.Any(match => match.Index == marker.Index))
                throw new BuildProblem(marker.Index, "write a pass as [Pass(threads, over: n)] void name(uint i) (or groups: g in place of over: n).");

        foreach (Match match in declared)
        {
            string name = match.Groups["name"].Value;
            if (name == "build")
                throw new BuildProblem(match.Index, "build is the schedule; give the pass another name.");
            if (passes.Any(pass => pass.Name == name))
                throw new BuildProblem(match.Index, $"the pass {name} is declared twice.");

            Group args = match.Groups["args"];
            string[] parts = SplitTop(args.Value);
            BuildExpression? over = null, groups = null;
            int threads = 0;
            int at = args.Index;
            for (int k = 0; k < parts.Length; at += parts[k].Length + 1, k++)
            {
                string part = parts[k];
                Match named = Regex.Match(part, @"^\s*(\w+)\s*:(.*)$", RegexOptions.Singleline);
                if (k == 0 && !named.Success)
                {
                    BuildValue value = Evaluate(ParseAt(part, at), at, Constant);
                    if (!value.Whole || value.Long < 1 || value.Long > 1024)
                        throw new BuildProblem(at, $"a pass runs 1 to 1024 threads a group; {name} asks for {value}.");
                    threads = (int)value.Long;
                    continue;
                }

                if (!named.Success || named.Groups[1].Value is not ("over" or "groups"))
                    throw new BuildProblem(at, "after the threads, a pass says over: n (a thread each) or groups: g (whole groups).");
                BuildExpression expression = ParseAt(named.Groups[2].Value, at);
                if (named.Groups[1].Value == "over")
                    over = expression;
                else
                    groups = expression;
            }

            if (threads == 0)
                throw new BuildProblem(args.Index, "say the threads a group first: [Pass(256, over: count)].");
            if ((over is null) == (groups is null))
                throw new BuildProblem(args.Index, "a pass says one of over: n (a thread each) or groups: g (whole groups).");

            Group parameters = match.Groups["params"];
            string[] declaredParameters = parameters.Value.Split(',', StringSplitOptions.TrimEntries);
            var arguments = new List<string>();
            for (int k = 0; k < declaredParameters.Length; k++)
            {
                Match parameter = Regex.Match(declaredParameters[k], @"^uint\s+(\w+)$");
                if (!parameter.Success)
                    throw new BuildProblem(parameters.Index,
                        $"a pass takes uint i (its thread), then up to {MaxArguments} uints its calls in build() pass it: {name}(uint i, uint round).");
                if (k > 0)
                    arguments.Add(parameter.Groups[1].Value);
            }

            if (arguments.Count > MaxArguments)
                throw new BuildProblem(parameters.Index, $"a pass takes at most {MaxArguments} uints after i.");
            passes.Add(new BuildPass(name, threads, over, groups, [.. arguments], match.Index, match.Index + match.Groups["attribute"].Length));
        }

        return passes;
    }

    // ---- the body as each stage compiles it --------------------------------------------

    /// <summary>
    /// The body with every buffer declared at the binding <paramref name="binding"/> gives it (by its
    /// place in <see cref="Buffers"/>) in descriptor set <paramref name="set"/>, and the <c>[Pass]</c>
    /// attributes and <c>build()</c> blanked: Slang never sees the schedule. <c>dispatch</c> (a pass's
    /// thread in its group, and its group) is declared where <c>build()</c> was. Lines stay where they were.
    /// </summary>
    public string Rewrite(string source, int set, Func<int, int> binding)
    {
        var spans = new List<(int Start, int End, string Text)>();
        for (int k = 0; k < Buffers.Count; k++)
        {
            BuildBuffer buffer = Buffers[k];
            string coherent = buffer.Coherent ? "globallycoherent " : "";
            spans.Add((buffer.Start, buffer.End, $"[[vk::binding({binding(k)}, {set})]] {coherent}RWStructuredBuffer<{buffer.Type}> {buffer.Name};"));
        }

        foreach (BuildPass pass in Passes)
            spans.Add((pass.Start, pass.End, ""));
        // In build()'s place, what a pass reads to know which thread it is (Slang finds it from anywhere in the body).
        spans.Add((_schedule.Start, _schedule.End, "struct Dispatch { uint thread; uint group; }; static Dispatch dispatch;"));

        var text = new StringBuilder();
        int at = 0;
        foreach ((int start, int end, string replaced) in spans.OrderBy(span => span.Start))
        {
            text.Append(source, at, start - at);
            text.Append(replaced);
            // The lines the original took, kept: errors after it land where they were written.
            int lines = source.AsSpan(start, end - start).Count('\n');
            text.Append('\n', lines);
            at = end;
        }

        text.Append(source, at, source.Length - at);
        return text.ToString();
    }

    // ---- text ----------------------------------------------------------------------

    /// <summary>The source with comments turned to spaces, every other character (and newline) where it was.</summary>
    private static string Blank(string source)
    {
        var text = new StringBuilder(source);
        for (int i = 0; i + 1 < text.Length; i++)
        {
            if (text[i] == '/' && text[i + 1] == '/')
            {
                for (; i < text.Length && text[i] != '\n'; i++)
                    text[i] = ' ';
            }
            else if (text[i] == '/' && text[i + 1] == '*')
            {
                text[i] = text[i + 1] = ' ';
                for (i += 2; i < text.Length && !(text[i] == '*' && i + 1 < text.Length && text[i + 1] == '/'); i++)
                    if (text[i] != '\n')
                        text[i] = ' ';
                if (i + 1 < text.Length)
                    text[i] = text[i + 1] = ' ';
                i++;
            }
        }

        return text.ToString();
    }

    /// <summary>The brace closing the one at <paramref name="open"/>, or -1.</summary>
    private static int Matching(string code, int open)
    {
        if (open < 0)
            return -1;
        int depth = 0;
        for (int i = open; i < code.Length; i++)
        {
            if (code[i] == '{')
                depth++;
            else if (code[i] == '}' && --depth == 0)
                return i;
        }

        return -1;
    }

    /// <summary>The text split at commas outside brackets.</summary>
    private static string[] SplitTop(string text)
    {
        var parts = new List<string>();
        int depth = 0, start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] is '(' or '[')
                depth++;
            else if (text[i] is ')' or ']')
                depth--;
            else if (text[i] == ',' && depth == 0)
            {
                parts.Add(text[start..i]);
                start = i + 1;
            }
        }

        parts.Add(text[start..]);
        return [.. parts];
    }
}
