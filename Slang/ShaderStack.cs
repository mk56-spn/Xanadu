using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// What an entry function makes the code around it do, in the order it runs: what a stack's files hold is read from
/// these, never from their names. A stack with <c>graph()</c> is described by it; <c>shade</c> and <c>deform</c> are
/// the item's own quad, and alone they are shorthand for one draw onto the frame.
/// </summary>
public enum StackRole
{
    /// <summary><c>void graph()</c>: the stack's resources and nodes, run in its order each frame.</summary>
    Graph,

    /// <summary><c>ItemVertex deform(ItemVertex v)</c>: run on every corner before it lands.</summary>
    Deform,

    /// <summary><c>float4 shade(…)</c>: run on every pixel the stack's quad covers.</summary>
    Shade,
}

/// <summary>One file of a stack: its name in the stack's folder (<c>wires.slang</c>) and its code.</summary>
public sealed record StackFile(string Name, string Text);

/// <summary>
/// A shader stack: a folder of <c>.slang</c> files compiled as one effect body (<see cref="Compose"/>), each under its own
/// <c>#line</c> mark so Slang's errors land on the file's own lines (<see cref="Label"/>). The files are named as their
/// author likes; what each does comes from the entry functions it declares (<see cref="RolesOf"/>), each of which the
/// stack has at most once. Blend isn't in the code: it is set where the stack is placed.
/// </summary>
public static partial class ShaderStack
{
    /// <summary>Every role, in the order it runs.</summary>
    public static readonly StackRole[] Roles = Enum.GetValues<StackRole>();

    /// <summary>A stack file's extension.</summary>
    public const string Extension = ".slang";

    /// <summary>The role's name as the editor shows it.</summary>
    public static string Title(StackRole role) => role.ToString().ToUpperInvariant();

    /// <summary>What blend is, at length, for hovering it.</summary>
    public const string BlendDescription =
        "Blend isn't in the code: it is how the stack's colour mixes with what is already on the frame (mix, add, subtract, " +
        "multiply, premultiplied, or a custom one), set where the stack is placed (on the item or its material). The same " +
        "stack can glow additively in one place and mix plainly in another.";

    /// <summary>The functions a role's entry declares that the stack runs: its entry (and a pool's <c>start</c>).</summary>
    public static string[] Entries(StackRole role) => role switch
    {
        StackRole.Graph => ["graph"],
        StackRole.Deform => ["deform"],
        StackRole.Shade => ["shade"],
        _ => [],
    };

    /// <summary>The roles a file's code takes on, in the order they run, from the entries it declares.</summary>
    public static IEnumerable<StackRole> RolesOf(string text)
    {
        string code = StripComments(text);
        return Roles.Where(role => EntryOf(role).Pattern.IsMatch(code));
    }

    /// <summary>The label Slang's errors carry for file <paramref name="file"/> of stack <paramref name="name"/>.</summary>
    public static string Label(string name, string file) => $"{name}/{file}".Replace("\"", "'");

    /// <summary>The entry a role is found by, as written, and the pattern that finds it.</summary>
    private static (string Entry, Regex Pattern) EntryOf(StackRole role) => role switch
    {
        StackRole.Graph => ("void graph()", GraphEntry()),
        StackRole.Deform => ("ItemVertex deform(ItemVertex v)", DeformEntry()),
        StackRole.Shade => ("float4 shade(Canvas canvas)", ShadeEntry()),
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    [GeneratedRegex(@"\bvoid\s+(?:graph|build)\s*\(\s*\)")]
    private static partial Regex GraphEntry();

    [GeneratedRegex(@"\bItemVertex\s+deform\s*\(")]
    private static partial Regex DeformEntry();

    [GeneratedRegex(@"\bfloat4\s+shade\s*\(")]
    private static partial Regex ShadeEntry();

    /// <summary>
    /// The files as one effect body, each under its own <c>#line</c> mark. Throws, worded as Slang's errors are (so the
    /// editor puts the problem on the file), when the stack can't work: no files, a name used twice or not a
    /// <c>.slang</c> one, or an entry declared twice.
    /// </summary>
    public static string Compose(string name, IReadOnlyList<StackFile> files)
    {
        Check(name, files);
        return Join(name, files);
    }

    /// <summary>
    /// The files as one body without checking them (<see cref="Compose"/> checks first): for reading the code as a whole
    /// (the editor's clips and warnings) while a file is half written. Files run in the order of the first role each
    /// holds (one with none goes with the graph), then by name.
    /// </summary>
    public static string Join(string name, IReadOnlyList<StackFile> files)
    {
        var composed = new StringBuilder();
        foreach (StackFile file in Ordered(files))
        {
            composed.Append("#line 1 \"").Append(Label(name, file.Name)).Append("\"\n");
            composed.Append(file.Text.ReplaceLineEndings("\n"));
            if (!file.Text.EndsWith('\n'))
                composed.Append('\n');
        }

        // Without a shade the item keeps its own colour, or, with a graph, draws only what the graph draws.
        HashSet<StackRole> roles = [.. files.SelectMany(file => RolesOf(file.Text))];
        if (!roles.Contains(StackRole.Shade))
            composed.Append(roles.Contains(StackRole.Graph)
                ? $"#line 1 \"{name} (no shade)\"\n{GraphAlone}\n"
                : $"#line 1 \"{name} (no shade)\"\nfloat4 shade(ItemInput i) {{ return i.colour; }}\n");
        return composed.ToString();
    }

    /// <summary>The files in the order they compose.</summary>
    public static IEnumerable<StackFile> Ordered(IEnumerable<StackFile> files) =>
        files.OrderBy(file => RolesOf(file.Text).DefaultIfEmpty(StackRole.Graph).First()).ThenBy(file => file.Name, StringComparer.Ordinal);

    /// <summary>The shade a stack with a graph and no shade gets: its quad is never drawn, only what the graph draws.</summary>
    public const string GraphAlone = "float4 shade(ItemInput i) { return float4(0); } // the graph alone";

    private static void Check(string name, IReadOnlyList<StackFile> files)
    {
        if (files.Count == 0)
            throw new InvalidOperationException($"{name}(1): error: a stack needs at least one file.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var held = new Dictionary<StackRole, string>();
        foreach (StackFile file in files)
        {
            if (!IsFileName(file.Name))
                throw new InvalidOperationException($"{Label(name, file.Name)}(1): error: a stack's file is a plain name ending in {Extension}.");
            if (!seen.Add(file.Name))
                throw new InvalidOperationException($"{Label(name, file.Name)}(1): error: the stack has two files called {file.Name}.");

            string code = StripComments(file.Text);
            foreach (StackRole role in Roles)
            {
                MatchCollection found = EntryOf(role).Pattern.Matches(code);
                if (found.Count == 0)
                    continue;
                if (found.Count > 1 || held.ContainsKey(role))
                    throw new InvalidOperationException(
                        $"{Label(name, file.Name)}({LineAt(code, found[^1].Index)}): error: the stack has one {Entries(role)[^1]}" +
                        (held.TryGetValue(role, out string? first) && first != file.Name ? $"; it is already in {first}." : "."));
                held[role] = file.Name;
            }
        }
    }

    /// <summary>Whether a name can be a stack's file: a plain name (letters, digits, _ and -) ending in <see cref="Extension"/>.</summary>
    public static bool IsFileName(string name) => FileNamePattern().IsMatch(name);

    [GeneratedRegex(@"^[A-Za-z0-9_-]+\.slang$")]
    private static partial Regex FileNamePattern();

    private static int LineAt(string text, int index) => 1 + text.AsSpan(0, index).Count('\n');

    private static string StripComments(string source) =>
        Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", match => new string('\n', match.Value.Count(c => c == '\n')), RegexOptions.Singleline),
            @"//[^\n]*", "");

    /// <summary>A new stack: one file with a shade that changes nothing much, explained.</summary>
    public static StackFile NewStack() => new("shade.slang", """
        // shade runs on every pixel of the item and returns its colour. A stack is every .slang file in its
        // folder, read as one: add files for its other parts, and a void graph() to run passes and draws.
        import xanadu;

        [Default(colours.gold)] uniform colour tint;

        float4 shade(ItemInput i, Canvas canvas)
        {
            colour sky = lerp(colours.midnightBlue, colours.darkSlateBlue, i.uv.y);
            let blob = Circle(120 + 20 * sin(time * 2));
            return lerp(sky, tint, blob.filled(canvas.centred, 2));
        }

        """);

    /// <summary>A new file for a stack: everything in it is in sight of the stack's other files, and theirs in its.</summary>
    public static string NewFile(string name) => $"""
        // {name}: read with the stack's other files as one. Declare resources ([Buffer], [Image], [Host]), passes
        // ([Pass], [Pixels], [Draw]) and functions here; void graph() (in any one file) runs them in order each frame.
        import xanadu;

        """;

    /// <summary>A name for a new file that the stack hasn't got: <c>part.slang</c>, <c>part2.slang</c>, …</summary>
    public static string FreeName(IEnumerable<string> taken, string stem = "part")
    {
        var names = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        for (int k = 1; ; k++)
        {
            string name = k == 1 ? stem + Extension : $"{stem}{k}{Extension}";
            if (!names.Contains(name))
                return name;
        }
    }

    // ---- on disk -------------------------------------------------------------------

    /// <summary>Whether a folder is a stack: it holds at least one <c>.slang</c> file.</summary>
    public static bool IsStack(string folder) =>
        Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*" + Extension).Any();

    /// <summary>The stack's files, by name.</summary>
    public static List<StackFile> Read(string folder) =>
    [
        .. Directory.EnumerateFiles(folder, "*" + Extension).Order(StringComparer.Ordinal)
            .Select(file => new StackFile(Path.GetFileName(file), File.ReadAllText(file))),
    ];

    /// <summary>Writes the files into the folder (made if needed) and deletes the <c>.slang</c> files it no longer has.</summary>
    public static void Write(string folder, IReadOnlyList<StackFile> files)
    {
        Directory.CreateDirectory(folder);
        foreach (StackFile file in files)
            File.WriteAllText(Path.Combine(folder, file.Name), file.Text);
        foreach (string old in Directory.EnumerateFiles(folder, "*" + Extension))
            if (!files.Any(file => string.Equals(file.Name, Path.GetFileName(old), StringComparison.OrdinalIgnoreCase)))
                File.Delete(old);
    }

    /// <summary>Every stack under <paramref name="root"/> (full paths), outside <paramref name="skip"/>, in order.</summary>
    public static IEnumerable<string> Under(string root, string? skip = null) =>
        Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .Where(folder => skip is null || !folder.StartsWith(skip, StringComparison.Ordinal))
            .Where(IsStack)
            .Order(StringComparer.Ordinal);
}
