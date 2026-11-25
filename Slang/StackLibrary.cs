using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// A schedule a library module declares: <c>[Schedule] void sort(uint n, RWStructuredBuffer&lt;uint&gt; keys, …)</c>,
/// a body of pass calls, loops and ifs that a brick's <c>build()</c> calls as if it were written there.
/// </summary>
/// <param name="Body">The body between its braces, as a range of the library's text.</param>
public sealed record LibrarySchedule(string Name, BuildParameter[] Parameters, int Start, (int Start, int End) Body);

/// <summary>
/// A library module that ships passes for BUILD bricks (<c>xanadu.bvh</c>, say): its
/// <c>[Pass]</c> functions, its <c>[Schedule]</c>s and its constants. A brick calls them in
/// <c>build()</c> by the module's last name (<c>bvh.link(n, keys, nodes, parents)</c>), lending its
/// own buffers by name; the library owns no buffer, so two bricks (or two calls) never share one.
/// A library pass is a plain function to Slang: the <c>[Pass]</c> attribute and every
/// <c>[Schedule]</c> are blanked before the module is built (<see cref="Prepared"/>).
/// </summary>
public sealed partial class StackLibrary
{
    private readonly BuildConstants _constants;

    /// <summary>What a brick calls it by: the module's last name, which is also its namespace.</summary>
    public string Name { get; }

    /// <summary>The module's import path (<c>xanadu.bvh</c>).</summary>
    public string Module { get; }

    /// <summary>The module's text, comments blanked: indexes in passes and schedules point into it.</summary>
    public string Code { get; }

    public IReadOnlyList<BuildPass> Passes { get; }

    public IReadOnlyList<LibrarySchedule> Schedules { get; }

    private StackLibrary(string module, string code)
    {
        Module = module;
        Name = module[(module.LastIndexOf('.') + 1)..];
        Code = code;
        _constants = new BuildConstants(code);
        Passes = BuildPasses.Read(code, _constants.Of, library: true);
        Schedules = ReadSchedules(code);
    }

    /// <summary>A constant of the module, worked out; null when there is none by that name.</summary>
    public BuildValue? Constant(string name) => _constants.Of(name);

    [GeneratedRegex(@"\[\s*Schedule\s*\]\s*(?:public\s+)?void\s+(?<name>\w+)\s*\((?<params>[^)]*)\)")]
    private static partial Regex ScheduleDeclaration();

    /// <summary>The module read, or null when it declares no pass and no schedule; throws <see cref="BuildProblem"/> when one is written wrong.</summary>
    public static StackLibrary? Of(string module, string source)
    {
        string code = BuildText.Blank(source);
        return BuildPasses.PassMarker().IsMatch(code) || ScheduleDeclaration().IsMatch(code) ? new StackLibrary(module, code) : null;
    }

    /// <summary>
    /// The module as Slang builds it: every <c>[Pass(…)]</c> attribute and every <c>[Schedule]</c>
    /// function blanked, lines kept. A module without either comes back as it was.
    /// </summary>
    public static string Prepared(string source)
    {
        string code = BuildText.Blank(source);
        if (!BuildPasses.PassMarker().IsMatch(code) && !ScheduleDeclaration().IsMatch(code))
            return source;
        var spans = new List<(int, int, string)>();
        foreach (Match pass in Regex.Matches(code, @"\[\s*Pass\s*\([^\]]*\)\s*\]"))
            spans.Add((pass.Index, pass.Index + pass.Length, ""));
        foreach (Match schedule in ScheduleDeclaration().Matches(code))
        {
            int close = BuildText.Matching(code, code.IndexOf('{', schedule.Index + schedule.Length));
            if (close > 0)
                spans.Add((schedule.Index, close + 1, ""));
        }

        return BuildText.Replaced(source, spans);
    }

    private static List<LibrarySchedule> ReadSchedules(string code)
    {
        var schedules = new List<LibrarySchedule>();
        foreach (Match match in ScheduleDeclaration().Matches(code))
        {
            string name = match.Groups["name"].Value;
            int open = code.IndexOf('{', match.Index + match.Length);
            int close = BuildText.Matching(code, open);
            if (open < 0 || close < 0)
                throw new BuildProblem(match.Index, $"the schedule {name} needs a body in braces.");
            var parameters = new List<BuildParameter>();
            foreach (string declared in match.Groups["params"].Value.Split(',', System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries))
            {
                string text = Regex.Replace(declared, @"\s+", " ");
                Match buffer = Regex.Match(text, @"^RWStructuredBuffer ?< ?(?<type>[\w:.]+) ?> (?<name>\w+)$");
                Match number = Regex.Match(text, @"^(?:uint|int) (\w+)$");
                if (buffer.Success)
                    parameters.Add(new BuildParameter(buffer.Groups["name"].Value, buffer.Groups["type"].Value));
                else if (number.Success)
                    parameters.Add(new BuildParameter(number.Groups[1].Value, null));
                else
                    throw new BuildProblem(match.Index, $"a schedule takes uints and RWStructuredBuffer<T>s: {name}(uint n, RWStructuredBuffer<uint> keys).");
            }

            schedules.Add(new LibrarySchedule(name, [.. parameters], match.Index, (open + 1, close)));
        }

        return schedules;
    }

    // ---- the shipped library's ------------------------------------------------------

    private static readonly System.Threading.Lock CacheLock = new();
    private static readonly Dictionary<string, (string Text, StackLibrary? Read)> Read = new(System.StringComparer.Ordinal);

    /// <summary>
    /// The library module a brick calls <paramref name="name"/> (the last part of its import path),
    /// read from <see cref="SlangShaders.LibraryPath"/>; null when there is none or it ships no passes.
    /// </summary>
    public static StackLibrary? Named(string name)
    {
        if (SlangShaders.LibraryPath is not { } library || !Directory.Exists(library))
            return null;
        foreach (string file in Directory.EnumerateFiles(library, name + ".slang", SearchOption.AllDirectories).Order(System.StringComparer.Ordinal))
        {
            string module = Path.ChangeExtension(Path.GetRelativePath(library, file), null).Replace(Path.DirectorySeparatorChar, '.');
            string text = File.ReadAllText(file);
            lock (CacheLock)
            {
                if (!Read.TryGetValue(file, out var known) || known.Text != text)
                    Read[file] = known = (text, Of(module, text));
                if (known.Read is not null)
                    return known.Read;
            }
        }

        return null;
    }
}
