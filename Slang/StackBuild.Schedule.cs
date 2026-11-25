using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Slang;

/// <summary>A BUILD brick's schedule: <c>build()</c> read into steps, and walked on the CPU each frame into the passes to dispatch.</summary>
public sealed partial class StackBuild
{
    /// <summary>How deep library schedules may call each other.</summary>
    private const int MaxDepth = 16;

    /// <summary>
    /// Where a schedule is being read: the brick's <c>build()</c> (no library, buffers named as the
    /// brick declares them) or a library's schedule (its own passes in sight, its buffer
    /// parameters bound to the brick buffers the call lent). <paramref name="Site"/> is where in the
    /// brick the outermost library call sits: what a library pass's compile errors point at.
    /// </summary>
    private sealed record Scope(StackLibrary? Library, IReadOnlyDictionary<string, int>? Lent, int Depth, int Site = -1);

    private List<BuildStep> ReadSchedule()
    {
        int open = _code.IndexOf('{', _schedule.Start);
        return Steps(_code, open + 1, _schedule.End - 1, new Scope(null, null, 0));
    }

    private List<BuildStep> Steps(string code, int start, int end, Scope scope)
    {
        var tokens = new BuildTokens(code[start..end], start);
        var steps = new List<BuildStep>();
        try
        {
            while (!tokens.AtEnd)
                steps.AddRange(Statement(tokens, scope));
        }
        catch (System.FormatException problem)
        {
            throw new BuildProblem(tokens.Peek.Index, $"build() lists passes: calls, lets, for loops and ifs over constants and uniforms ({problem.Message}).");
        }

        return steps;
    }

    private BuildStep[] Statement(BuildTokens tokens, Scope scope)
    {
        BuildToken first = tokens.Peek;
        if (tokens.Take(";"))
            return [];
        if (tokens.Take("{"))
        {
            var steps = new List<BuildStep>();
            while (!tokens.Take("}"))
                steps.AddRange(Statement(tokens, scope));
            return [.. steps];
        }

        if (tokens.Take("if"))
        {
            tokens.Expect("(");
            BuildExpression test = tokens.Expression();
            tokens.Expect(")");
            BuildStep[] yes = Statement(tokens, scope);
            BuildStep[] no = tokens.Take("else") ? Statement(tokens, scope) : [];
            return [new BuildBranch(first.Index, test, yes, no)];
        }

        if (tokens.Take("for"))
            return [Loop(tokens, scope, first)];

        if (tokens.Peek.Text is "uint" or "int" or "float" or "bool")
        {
            tokens.Next();
            string variable = tokens.ExpectName();
            tokens.Expect("=");
            BuildExpression value = tokens.Expression();
            tokens.Expect(";");
            return [new BuildLet(first.Index, variable, value)];
        }

        string name = tokens.ExpectName();
        if (name == "present" && scope.Library is null && tokens.Is("("))
            return [Present(tokens, first)];
        StackLibrary? library = scope.Library;
        if (tokens.Take("."))
        {
            library = Library(name, first.Index);
            name = tokens.ExpectName();
        }

        return [Call(tokens, scope, library, name, first)];
    }

    /// <summary><c>present(colour)</c> or <c>present(colour, depth)</c>: images the graph declares, the colour four channels, the depth one float.</summary>
    private BuildPresent Present(BuildTokens tokens, BuildToken first)
    {
        tokens.Expect("(");
        int colour = Image(tokens, "the colour to show", ["rgba16f", "rgba8", "rgba32f"]);
        int depth = -1;
        if (tokens.Take(","))
            depth = Image(tokens, "its depth, pixels into the screen", ["r32f", "r16f"]);
        tokens.Expect(")");
        tokens.Expect(";");
        Presents = true;
        return new BuildPresent(first.Index, colour, depth);
    }

    private int Image(BuildTokens tokens, string what, string[] formats)
    {
        BuildToken given = tokens.Peek;
        string name = tokens.ExpectName();
        int image = Images.ToList().FindIndex(declared => declared.Name == name);
        if (image < 0)
            throw new BuildProblem(given.Index, $"present takes images for {what}; {name} isn't an [Image] this graph declares.");
        if (!formats.Contains(Images[image].Format))
            throw new BuildProblem(given.Index, $"present takes a {string.Join(" or ", formats)} image for {what}; {name} is {Images[image].Format}.");
        return image;
    }

    private BuildLoop Loop(BuildTokens tokens, Scope scope, BuildToken first)
    {
        tokens.Expect("(");
        if (tokens.Peek.Text is "uint" or "int")
            tokens.Next();
        string variable = tokens.ExpectName();
        tokens.Expect("=");
        BuildExpression start = tokens.Expression();
        tokens.Expect(";");
        BuildExpression test = tokens.Expression();
        tokens.Expect(";");
        BuildExpression? by = null;
        string op;
        if (tokens.Is("++") || tokens.Is("--"))
        {
            op = tokens.Next().Text;
            if (tokens.ExpectName() != variable)
                throw new System.FormatException($"the loop steps {variable}");
        }
        else
        {
            if (tokens.ExpectName() != variable)
                throw new System.FormatException($"the loop steps {variable}");
            op = tokens.Next().Text;
            if (op is not ("++" or "--"))
            {
                if (op is not ("+=" or "-=" or "*=" or "/=" or "<<=" or ">>="))
                    throw new System.FormatException($"a loop steps with ++, --, +=, -=, *=, /=, <<= or >>=, not {op}");
                by = tokens.Expression();
            }
        }

        tokens.Expect(")");
        return new BuildLoop(first.Index, variable, start, test, op, by, Statement(tokens, scope));
    }

    private static StackLibrary Library(string name, int index)
    {
        try
        {
            return StackLibrary.Named(name)
                   ?? throw new BuildProblem(index, $"no library module {name} ships passes: a library pass is [Pass] in a module under Library, called as {name}.pass(…).");
        }
        catch (BuildProblem problem) when (problem.Index != index)
        {
            throw new BuildProblem(index, $"the library {name} is written wrong: {problem.Message}");
        }
    }

    private BuildStep Call(BuildTokens tokens, Scope scope, StackLibrary? library, string name, BuildToken first)
    {
        string called = library is null ? name : $"{library.Name}.{name}";
        if (library is null)
        {
            int own = _passes.FindIndex(pass => pass.Library is null && pass.Name == name);
            if (own < 0)
                throw new BuildProblem(first.Index, $"{name} isn't a pass: mark it [Pass(threads, over: n)], or call only passes in build().");
            BuildExpression[] arguments = Arguments(tokens, scope, called, _passes[own].Parameters, first.Index, out _);
            return new BuildCall(first.Index, own, arguments);
        }

        if (library.Passes.FirstOrDefault(pass => pass.Name == name) is { } declared)
        {
            BuildExpression[] arguments = Arguments(tokens, scope, called, declared.Parameters, first.Index, out int[] bound);
            int at = _passes.FindIndex(pass => pass.Library == library && pass.Name == name && pass.Bound.SequenceEqual(bound));
            if (at < 0)
            {
                at = _passes.Count;
                int site = scope.Site >= 0 ? scope.Site : first.Index;
                _passes.Add(declared with { Library = library, Bound = bound, Start = site, End = site });
            }

            return new BuildCall(first.Index, at, arguments);
        }

        if (library.Schedules.FirstOrDefault(schedule => schedule.Name == name) is { } inlined)
        {
            if (scope.Depth >= MaxDepth)
                throw new BuildProblem(first.Index, $"library schedules call each other more than {MaxDepth} deep.");
            BuildExpression[] arguments = Arguments(tokens, scope, called, inlined.Parameters, first.Index, out int[] bound);
            BuildParameter[] buffers = [.. inlined.Parameters.Where(parameter => parameter.IsBuffer)];
            var lent = new Dictionary<string, int>();
            for (int k = 0; k < buffers.Length; k++)
                lent[buffers[k].Name] = bound[k];
            List<BuildStep> body;
            try
            {
                body = Steps(library.Code, inlined.Body.Start, inlined.Body.End, new Scope(library, lent, scope.Depth + 1, scope.Site >= 0 ? scope.Site : first.Index));
            }
            catch (BuildProblem problem)
            {
                throw new BuildProblem(first.Index, $"in {called}: {problem.Message}");
            }

            string[] numbers = [.. inlined.Parameters.Where(parameter => !parameter.IsBuffer).Select(parameter => parameter.Name)];
            return new BuildInline(first.Index, library, called, numbers, arguments, [.. body]);
        }

        throw new BuildProblem(first.Index, $"the library {library.Name} has no pass or schedule {name}.");
    }

    /// <summary>
    /// A call's arguments: the uints as expressions (returned), the buffers as the brick buffer each
    /// lends (<paramref name="bound"/>, in order), checked against the parameters.
    /// </summary>
    private BuildExpression[] Arguments(BuildTokens tokens, Scope scope, string called, BuildParameter[] parameters, int index, out int[] bound)
    {
        tokens.Expect("(");
        var numbers = new List<BuildExpression>();
        var buffers = new List<int>();
        for (int k = 0; !tokens.Is(")"); k++)
        {
            if (k > 0)
                tokens.Expect(",");
            if (k >= parameters.Length)
                throw new BuildProblem(index, Takes(called, parameters));
            if (!parameters[k].IsBuffer)
            {
                numbers.Add(tokens.Expression());
                continue;
            }

            BuildToken given = tokens.Peek;
            string name = tokens.ExpectName();
            int buffer = scope.Lent is { } lent
                ? lent.GetValueOrDefault(name, -1)
                : Buffers.ToList().FindIndex(declared => declared.Name == name);
            if (buffer < 0)
                throw new BuildProblem(given.Index, $"{called} takes a buffer for {parameters[k].Name}; {name} isn't one {(scope.Lent is null ? "this brick declares" : "the schedule was lent")}.");
            if (Element(Buffers[buffer].Type) != Element(parameters[k].BufferType!))
                throw new BuildProblem(given.Index,
                    $"{called} takes RWStructuredBuffer<{parameters[k].BufferType}> for {parameters[k].Name}; {Buffers[buffer].Name} holds {Buffers[buffer].Type}.");
            buffers.Add(buffer);
        }

        tokens.Expect(")");
        tokens.Expect(";");
        if (numbers.Count + buffers.Count != parameters.Length)
            throw new BuildProblem(index, Takes(called, parameters));
        bound = [.. buffers];
        return [.. numbers];
    }

    /// <summary>A type's own name, without the module or namespace before it: <c>bvh.Node</c> is <c>Node</c>.</summary>
    private static string Element(string type) => type[(type.LastIndexOfAny(['.', ':']) + 1)..];

    private static string Takes(string called, BuildParameter[] parameters) =>
        $"{called} takes {parameters.Length} argument{(parameters.Length == 1 ? "" : "s")} ({string.Join(", ", parameters.Select(parameter => parameter.Name))}).";
}
