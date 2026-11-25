using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xanadu.Core.Theme;
using Xanadu.Slang;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>The compile off the frame, and its problems and colour conversions spread back over the texts they are on.</summary>
internal sealed partial class ShaderCodeEditor
{
    /// <summary>Compiles the text off the frame, so typing never waits on Slang; a stack is composed there too.</summary>
    private void Compile()
    {
        int version = Version;
        (string joined, List<Placed> places) = Joined();
        string? stack = _stack;
        List<StackFile> files = Files;
        Compiling?.Invoke(joined);

        if (!_everCompiled)
            (_status, _statusColour) = ("compiling…", Palette.TextMuted);
        IEditorPreview view = _view;
        // The warnings are read beside the compile, off the frame too; the type names and costs are never changed after start.
        HashSet<string> typeNames = _typeNames;
        Dictionary<string, float> shapeCosts = _shapeCosts;
        Task<List<SlangProblem>> lint = Task.Run(() => SlangLint.Check(joined, typeNames, shapeCosts));
        _compiling = Task.Run(() =>
        {
            var clock = Stopwatch.StartNew();
            SlangShaders.ResetSlangTime();
            string source = joined;
            try
            {
                // Composing checks the stack's shape (each entry once, the files named plainly), worded as Slang's errors are.
                if (stack is not null)
                    source = ShaderStack.Compose(stack, files);
                var build = (PreviewBuild?)view.Compile(source);
                return new Compiled(version, source, places, build, null, clock.ElapsedMilliseconds, SlangShaders.SlangMilliseconds, lint.Result);
            }
            catch (Exception exception)
            {
                return new Compiled(version, source, places, null, exception.Message, clock.ElapsedMilliseconds, SlangShaders.SlangMilliseconds, lint.Result);
            }
        });
    }

    private void Finish()
    {
        if (_compiling is not { IsCompleted: true } done)
            return;

        _compiling = null;
        Compiled compiled = done.Result;
        _compiledVersion = compiled.Version;
        string? error = compiled.Error;
        SpreadConversions(compiled.Places, compiled.Source, SlangShaders.ColourConversionsOf(compiled.Source));
        string said = "";
        if (error is null)
        {
            try
            {
                said = _view.Show(compiled.Build!);
                _compiledStack = compiled.Build!.Stack;
            }
            catch (Exception exception)
            {
                error = $"{_view.Label}(1): error: the GPU refused it: {exception.Message}";
            }
        }

        SpreadProblems(compiled.Places, compiled.Source, error, compiled.Lint);
        ListProblems();
        if (error is not null)
        {
            int count = _shownProblems.Count(problem => !problem.Warning);
            (_status, _statusColour) = ($"{count} problem{(count == 1 ? "" : "s")}", Palette.DangerBright);
            return;
        }

        _tuning.Use(compiled.Build!.Params);
        _everCompiled = true;
        int warnings = _shownProblems.Count;
        string warned = warnings == 0 ? "" : $" · {warnings} warning{(warnings == 1 ? "" : "s")}";
        long ms = compiled.Ms;
        double slangMs = compiled.SlangMs;
        // Slang's own share apart from ours (wrapping, rewrites, reflection); none at all when the cache had it.
        string split = slangMs > 0 ? $" (ours {Math.Max(0, ms - (long)slangMs)} · slang {(long)slangMs})" : " (cached)";
        // The library from source is the slow path: say why it couldn't be precompiled.
        if (SlangShaders.ModuleProblem is { } unbuilt)
            split += $" · library not precompiled: {unbuilt}";
        (_status, _statusColour) = ($"compiled in {ms} ms{split}{warned}{said}", warnings == 0 ? Palette.Success : Palette.Warning);
    }

    /// <summary>Slang's problems and the editor's warnings together, by line; Slang's first on a shared line.</summary>
    private static List<SlangProblem> WithLint(List<SlangProblem> problems, IEnumerable<SlangProblem> lint)
    {
        var all = new List<SlangProblem>(problems);
        foreach (SlangProblem warning in lint)
        {
            if (!problems.Exists(problem => problem.Line == warning.Line))
                all.Add(warning);
        }

        all.Sort((a, b) => a.Line.CompareTo(b.Line));
        return all;
    }

    /// <summary>The last compile's colour conversions, each on the text it is in, at its own line.</summary>
    private void SpreadConversions(List<Placed> places, string source, IReadOnlyList<ColourConversion> conversions)
    {
        foreach (Tab file in _files)
            file.Conversions = [];
        string[] lines = source.Split('\n');
        foreach (ColourConversion conversion in conversions)
        {
            if (conversion.Line >= lines.Length || Unjoin(places, conversion.Line + 1) is not ({ } tab, int own))
                continue;
            string line = lines[conversion.Line];
            string covered = line[Math.Min(conversion.Column, line.Length)..Math.Min(line.Length, conversion.Column + conversion.Length)];
            tab.Conversions.Add((conversion with { Line = own - 1 }, covered));
        }
    }

    /// <summary>
    /// The last compile's problems, each on the text whose label it carries (a file's own
    /// lines), with the warnings read over the whole; the ones on none of them are global.
    /// </summary>
    private void SpreadProblems(List<Placed> places, string source, string? error, List<SlangProblem> lint)
    {
        foreach (Tab file in _files)
            file.Problems = [];
        _looksVersion = -1;
        string[] lines = source.Split('\n');
        // Slang's wording of each problem a text took, before a missing ; is moved and reworded.
        var placed = new List<string>();
        foreach (Placed place in places)
        {
            if (!_files.Contains(place.Tab))
                continue;
            string[] own = lines[Math.Min(place.Start, lines.Length)..Math.Min(lines.Length, place.Start + place.Count)];
            List<SlangProblem> slang = error is null ? [] : [.. SlangProblems.Parse(error, LabelOf(place.Tab), own).Where(problem => problem.Line > 0)];
            IEnumerable<SlangProblem> warnings = lint
                .Select(warning => (Place: Unjoin(places, warning.Line), Warning: warning))
                .Where(found => found.Place?.Tab == place.Tab)
                .Select(found => found.Warning with { Line = found.Place!.Value.Line });
            place.Tab.Problems = WithLint(slang, warnings);
            if (error is not null)
                placed.AddRange(SlangProblems.Parse(error, LabelOf(place.Tab)).Where(problem => problem.Line > 0).Select(problem => problem.Message));
        }

        // Parsed under no label, every problem is at line 0: the ones no text took are the global ones.
        _global = error is null ? [] : [.. SlangProblems.Parse(error, "").Where(problem => !placed.Contains(problem.Message))];
    }
}
