using System.Collections.Generic;

namespace Xanadu.Slang;

/// <summary>A BUILD brick's schedule walked on the CPU each frame into the passes to dispatch.</summary>
public sealed partial class StackBuild
{
    /// <summary>One pass to dispatch: which, its groups, the elements a thread each (0 for whole groups), and its arguments.</summary>
    public readonly record struct Run(int Pass, uint Groups, uint Over, uint A, uint B, uint C, uint D)
    {
        /// <summary>A draw's targets, in order (<see cref="A"/> to <see cref="D"/>; <see cref="None"/> past its last).</summary>
        public uint Target(int k) => k switch { 0 => A, 1 => B, 2 => C, 3 => D, _ => None };

        /// <summary>The pass a <c>present</c> runs as: the images shown are <see cref="A"/> (colour) and <see cref="B"/> (depth, or <see cref="None"/>).</summary>
        public const int Present = -1;

        /// <summary>No image.</summary>
        public const uint None = uint.MaxValue;

        /// <summary>The frame, as a draw's target: the draw is an exit.</summary>
        public const uint Frame = uint.MaxValue - 1;

        /// <summary>Whether it is a draw onto the frame.</summary>
        public bool OntoFrame => A == Frame;

        public bool Presents => Pass == Present;
    }

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
                    runs.Add(Dispatch(call, lookup, runs.Count));
                    break;
                case BuildLet let:
                    variables[let.Variable] = BuildPasses.Evaluate(let.Value, let.Index, lookup);
                    break;
                case BuildBranch branch:
                    Walk(BuildPasses.Evaluate(branch.Test, branch.Index, lookup).IsTrue ? branch.Yes : branch.No, lookup, variables, runs);
                    break;
                case BuildLoop loop:
                    WalkLoop(loop, lookup, variables, runs);
                    break;
                case BuildInline inline:
                    WalkInline(inline, lookup, runs);
                    break;
                case BuildDraw draw:
                    if (runs.Count >= MaxCalls)
                        throw new BuildProblem(draw.Index, $"graph() runs more than {MaxCalls} passes a frame.");
                    runs.Add(new Run(draw.Pass, 0, 0, Target(draw, 0), Target(draw, 1), Target(draw, 2), Target(draw, 3)));
                    break;
                case BuildPresent present:
                    runs.Add(new Run(Run.Present, 0, 0, (uint)present.Colour, present.Depth < 0 ? Run.None : (uint)present.Depth, 0, 0));
                    break;
            }
        }
    }

    /// <summary>A draw's <paramref name="k"/>th colour target, <see cref="Run.None"/> past its last.</summary>
    private static uint Target(BuildDraw draw, int k) =>
        k >= draw.Targets.Length ? Run.None : draw.Targets[k] == BuildDraw.Frame ? Run.Frame : (uint)draw.Targets[k];

    private Run Dispatch(BuildCall call, Func<string, BuildValue?> lookup, int count)
    {
        if (count >= MaxCalls)
            throw new BuildProblem(call.Index, $"build() runs more than {MaxCalls} passes a frame.");
        BuildPass pass = Passes[call.Pass];
        Span<uint> args = stackalloc uint[MaxArguments];
        for (int k = 0; k < call.Arguments.Length; k++)
            args[k] = (uint)BuildPasses.Evaluate(call.Arguments[k], call.Index, lookup).Long;

        // A library's pass sizes itself from its own parameters and constants, never the brick's names.
        Func<string, BuildValue?> sizes = lookup;
        int at = pass.Start;
        if (pass.Library is { } library)
        {
            var given = new Dictionary<string, BuildValue>();
            for (int k = 0; k < pass.Arguments.Length; k++)
                given[pass.Arguments[k]] = BuildValue.Of(args[k]);
            sizes = name => given.TryGetValue(name, out BuildValue value) ? value : library.Constant(name);
            at = call.Index;
        }

        if (pass.Pixels is not null)
            return new Run(call.Pass, 0, 0, args[0], args[1], args[2], args[3]);
        uint over = 0, groups;
        if (pass.Over is { } elements)
        {
            over = (uint)Math.Max(0, BuildPasses.Evaluate(elements, at, sizes).Long);
            groups = (uint)((over + pass.Threads - 1) / pass.Threads);
        }
        else
            groups = (uint)Math.Max(0, BuildPasses.Evaluate(pass.Groups!, at, sizes).Long);
        return new Run(call.Pass, groups, over, args[0], args[1], args[2], args[3]);
    }

    private void WalkLoop(BuildLoop loop, Func<string, BuildValue?> lookup, Dictionary<string, BuildValue> variables, List<Run> runs)
    {
        bool had = variables.TryGetValue(loop.Variable, out BuildValue outer);
        variables[loop.Variable] = BuildPasses.Evaluate(loop.Start, loop.Index, lookup);
        for (int turns = 0; BuildPasses.Evaluate(loop.Test, loop.Index, lookup).IsTrue; turns++)
        {
            if (turns >= MaxIterations)
                throw new BuildProblem(loop.Index, $"this loop runs more than {MaxIterations} times.");
            Walk(loop.Body, lookup, variables, runs);
            long v = variables[loop.Variable].Long;
            long by = loop.By is null ? 1 : BuildPasses.Evaluate(loop.By, loop.Index, lookup).Long;
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
    }

    /// <summary>A library's schedule: its uints given, then its body with only those and the library's constants in sight.</summary>
    private void WalkInline(BuildInline inline, Func<string, BuildValue?> lookup, List<Run> runs)
    {
        var variables = new Dictionary<string, BuildValue>();
        for (int k = 0; k < inline.Parameters.Length; k++)
            variables[inline.Parameters[k]] = BuildPasses.Evaluate(inline.Arguments[k], inline.Index, lookup);
        BuildValue? Inside(string name) =>
            variables.TryGetValue(name, out BuildValue value) ? value : inline.Library.Constant(name);
        try
        {
            Walk(inline.Body, Inside, variables, runs);
        }
        catch (BuildProblem problem) when (problem.Index != inline.Index)
        {
            throw new BuildProblem(inline.Index, $"in {inline.Name}: {problem.Message}");
        }
    }
}
