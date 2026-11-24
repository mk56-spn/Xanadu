using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Slang;

/// <summary>A BUILD brick's schedule: <c>build()</c> read into steps, and walked on the CPU each frame into the passes to dispatch.</summary>
public sealed partial class StackBuild
{
    private List<BuildStep> ReadSchedule()
    {
        int open = _code.IndexOf('{', _schedule.Start);
        var tokens = new BuildTokens(_code[(open + 1)..(_schedule.End - 1)], open + 1);
        var steps = new List<BuildStep>();
        try
        {
            while (!tokens.AtEnd)
                steps.AddRange(Statement(tokens));
        }
        catch (FormatException problem)
        {
            throw new BuildProblem(tokens.Peek.Index, $"build() lists passes: calls, for loops and ifs over constants and uniforms ({problem.Message}).");
        }

        return steps;
    }

    private BuildStep[] Statement(BuildTokens tokens)
    {
        BuildToken first = tokens.Peek;
        if (tokens.Take(";"))
            return [];
        if (tokens.Take("{"))
        {
            var steps = new List<BuildStep>();
            while (!tokens.Take("}"))
                steps.AddRange(Statement(tokens));
            return [.. steps];
        }

        if (tokens.Take("if"))
        {
            tokens.Expect("(");
            BuildExpression test = tokens.Expression();
            tokens.Expect(")");
            BuildStep[] yes = Statement(tokens);
            BuildStep[] no = tokens.Take("else") ? Statement(tokens) : [];
            return [new BuildBranch(first.Index, test, yes, no)];
        }

        if (tokens.Take("for"))
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
                    throw new FormatException($"the loop steps {variable}");
            }
            else
            {
                if (tokens.ExpectName() != variable)
                    throw new FormatException($"the loop steps {variable}");
                op = tokens.Next().Text;
                if (op is not ("++" or "--"))
                {
                    if (op is not ("+=" or "-=" or "*=" or "/=" or "<<=" or ">>="))
                        throw new FormatException($"a loop steps with ++, --, +=, -=, *=, /=, <<= or >>=, not {op}");
                    by = tokens.Expression();
                }
            }

            tokens.Expect(")");
            return [new BuildLoop(first.Index, variable, start, test, op, by, Statement(tokens))];
        }

        string name = tokens.ExpectName();
        int pass = Passes.ToList().FindIndex(declared => declared.Name == name);
        if (pass < 0)
            throw new BuildProblem(first.Index, $"{name} isn't a pass: mark it [Pass(threads, over: n)], or call only passes in build().");
        tokens.Expect("(");
        var arguments = new List<BuildExpression>();
        if (!tokens.Is(")"))
        {
            arguments.Add(tokens.Expression());
            while (tokens.Take(","))
                arguments.Add(tokens.Expression());
        }

        tokens.Expect(")");
        tokens.Expect(";");
        if (arguments.Count != Passes[pass].Arguments.Length)
            throw new BuildProblem(first.Index,
                $"{name} takes {Passes[pass].Arguments.Length} argument{(Passes[pass].Arguments.Length == 1 ? "" : "s")} after its thread ({string.Join(", ", Passes[pass].Arguments.Prepend("i"))}); this call passes {arguments.Count}.");
        return [new BuildCall(first.Index, pass, [.. arguments])];
    }

    /// <summary>One pass to dispatch: which, its groups, the elements a thread each (0 for whole groups), and its arguments.</summary>
    public readonly record struct Run(int Pass, uint Groups, uint Over, uint A, uint B, uint C, uint D);

    /// <summary>
    /// This frame's passes in order, the uniforms read through <paramref name="uniform"/> (null for
    /// a name that isn't one); appended to <paramref name="runs"/>. Throws <see cref="BuildProblem"/>
    /// for a schedule that can't run: a name nothing knows, too many passes, a loop that doesn't end.
    /// </summary>
    public void Walk(Func<string, BuildValue?> uniform, List<Run> runs)
    {
        var variables = new Dictionary<string, BuildValue>();
        BuildValue? Lookup(string name) =>
            variables.TryGetValue(name, out BuildValue value) ? value : uniform(name) ?? Constant(name);
        Walk(Schedule, Lookup, variables, runs);
    }

    private void Walk(IReadOnlyList<BuildStep> steps, Func<string, BuildValue?> lookup, Dictionary<string, BuildValue> variables, List<Run> runs)
    {
        foreach (BuildStep step in steps)
        {
            switch (step)
            {
                case BuildCall call:
                {
                    if (runs.Count >= MaxCalls)
                        throw new BuildProblem(call.Index, $"build() runs more than {MaxCalls} passes a frame.");
                    BuildPass pass = Passes[call.Pass];
                    Span<uint> args = stackalloc uint[MaxArguments];
                    for (int k = 0; k < call.Arguments.Length; k++)
                        args[k] = (uint)Evaluate(call.Arguments[k], call.Index, lookup).Long;
                    uint over = 0, groups;
                    if (pass.Over is { } elements)
                    {
                        long n = Evaluate(elements, pass.Start, lookup).Long;
                        over = (uint)Math.Max(0, n);
                        groups = (uint)((over + pass.Threads - 1) / pass.Threads);
                    }
                    else
                        groups = (uint)Math.Max(0, Evaluate(pass.Groups!, pass.Start, lookup).Long);
                    runs.Add(new Run(call.Pass, groups, over, args[0], args[1], args[2], args[3]));
                    break;
                }
                case BuildBranch branch:
                    Walk(Evaluate(branch.Test, branch.Index, lookup).IsTrue ? branch.Yes : branch.No, lookup, variables, runs);
                    break;
                case BuildLoop loop:
                {
                    bool had = variables.TryGetValue(loop.Variable, out BuildValue outer);
                    variables[loop.Variable] = Evaluate(loop.Start, loop.Index, lookup);
                    for (int turns = 0; Evaluate(loop.Test, loop.Index, lookup).IsTrue; turns++)
                    {
                        if (turns >= MaxIterations)
                            throw new BuildProblem(loop.Index, $"this loop runs more than {MaxIterations} times.");
                        Walk(loop.Body, lookup, variables, runs);
                        long v = variables[loop.Variable].Long;
                        long by = loop.By is null ? 1 : Evaluate(loop.By, loop.Index, lookup).Long;
                        variables[loop.Variable] = BuildValue.Of(loop.Op switch
                        {
                            "++" => v + 1,
                            "--" => v - 1,
                            "+=" => v + by,
                            "-=" => v - by,
                            "*=" => v * by,
                            "/=" => by == 0 ? throw new BuildProblem(loop.Index, "the loop divides by zero.") : v / by,
                            "<<=" => v << (int)by,
                            _ => v >> (int)by,
                        });
                    }

                    if (had)
                        variables[loop.Variable] = outer;
                    else
                        variables.Remove(loop.Variable);
                    break;
                }
            }
        }
    }
}
