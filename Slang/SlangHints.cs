using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>A parameter name drawn in front of an argument, which isn't in the text.</summary>
public readonly record struct InlayHint(int Column, string Label);

/// <summary>
/// Parameter names for calls into the shader library, shown as inlay hints:
/// <c>Box(float2(28.0), 10.0)</c> reads <c>Box(size: float2(28.0), radius: 10.0)</c>.
/// Slang has no named arguments, so the names live only in the editor.
/// </summary>
/// <remarks>
/// The table comes from the library's own <c>public</c> constructors, functions
/// and methods, by name and argument count, read as text (the library's
/// declarations are regular). Calls are matched by name alone, so a name that
/// means different parameters in two places gets no hint rather than a wrong
/// one. Hints show for calls with two or more arguments, or one numeric literal.
/// </remarks>
public sealed partial class SlangHints
{
    /// <summary>Parameter names by (name, argument count); null where two declarations disagree.</summary>
    private readonly Dictionary<(string Name, int Count), string[]?> _table = new();

    [GeneratedRegex(@"public\s+struct\s+(\w+)")]
    private static partial Regex Struct();

    [GeneratedRegex(@"public\s+__init\s*\(([^)]*)\)")]
    private static partial Regex Constructor();

    [GeneratedRegex(@"public\s+(?:static\s+)?[\w<>:, ]+?\s+(\w+)\s*(?:<[^>()]*>)?\s*\(([^)]*)\)")]
    private static partial Regex Function();

    [GeneratedRegex(@"\b([A-Za-z_]\w*)\s*\(")]
    private static partial Regex Call();

    [GeneratedRegex(@"^-?\d+(\.\d*)?[fF]?$|^-?\.\d+[fF]?$")]
    private static partial Regex Number();

    public int Count => _table.Count;

    /// <summary>The library's struct names (shapes, <c>Bezier</c>, …), for colouring.</summary>
    public HashSet<string> Types { get; } = new();

    /// <summary>The library's type aliases (<c>colour</c>): other names for built-in types, so coloured as those are.</summary>
    public HashSet<string> Aliases { get; } = new();

    /// <summary>The attributes the library declares (<c>DefaultAttribute</c> → <c>Default</c>), for colouring apart from Slang's.</summary>
    public HashSet<string> Attributes { get; } = new();

    /// <summary>The library's namespaces (<c>color</c>, <c>ease</c>, …), for colouring.</summary>
    public HashSet<string> Namespaces { get; } = new();

    [GeneratedRegex(@"public\s+typealias\s+(\w+)")]
    private static partial Regex TypeAlias();

    [GeneratedRegex(@"public\s+namespace\s+(\w+)")]
    private static partial Regex NamespaceDeclaration();

    /// <summary>Every signature in the <c>.slang</c> files under a folder.</summary>
    public static SlangHints FromLibrary(string folder)
    {
        var hints = new SlangHints();
        if (Directory.Exists(folder))
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*.slang", SearchOption.AllDirectories))
                hints.Read(File.ReadAllText(file));
        }

        return hints;
    }

    /// <summary>Adds the declarations in one library file.</summary>
    public void Read(string source)
    {
        source = WithoutComments(source);
        var structs = Struct().Matches(source).Select(match => (match.Index, Name: match.Groups[1].Value)).ToList();
        foreach (var declared in structs)
        {
            Types.Add(declared.Name);
            if (declared.Name.Length > "Attribute".Length && declared.Name.EndsWith("Attribute", StringComparison.Ordinal))
                Attributes.Add(declared.Name[..^"Attribute".Length]);
        }
        foreach (Match alias in TypeAlias().Matches(source))
            Aliases.Add(alias.Groups[1].Value);
        foreach (Match name in NamespaceDeclaration().Matches(source))
            Namespaces.Add(name.Groups[1].Value);
        foreach (Match match in Constructor().Matches(source))
        {
            string? owner = structs.LastOrDefault(s => s.Index < match.Index).Name;
            if (owner is not null)
                Add(owner, match.Groups[1].Value);
        }

        foreach (Match match in Function().Matches(source))
        {
            if (match.Groups[1].Value != "__init")
                Add(match.Groups[1].Value, match.Groups[2].Value);
        }
    }

    /// <summary>The hints for one line of code, left to right.</summary>
    public List<InlayHint> For(string line)
    {
        var hints = new List<InlayHint>();
        int comment = line.IndexOf("//", StringComparison.Ordinal);
        string code = comment >= 0 ? line[..comment] : line;
        foreach (Match call in Call().Matches(code))
        {
            int open = call.Index + call.Length - 1;
            List<(int Start, string Text)>? args = Arguments(code, open);
            if (args is null || args.Count == 0)
                continue;
            if (!_table.TryGetValue((call.Groups[1].Value, args.Count), out string[]? names) || names is null)
                continue;
            if (args.Count == 1 && !Number().IsMatch(args[0].Text))
                continue;

            for (int i = 0; i < args.Count; i++)
            {
                // Naming an argument that already says its name is noise.
                if (args[i].Text != names[i])
                    hints.Add(new InlayHint(args[i].Start, names[i] + ":"));
            }
        }

        hints.Sort((a, b) => a.Column.CompareTo(b.Column));
        return hints;
    }

    private void Add(string name, string parameters)
    {
        List<string> parts = SplitTopLevel(parameters, 0, parameters.Length).Select(p => p.Text).Where(p => p.Length > 0).ToList();
        string[] names = parts.Select(NameOf).ToArray();
        int required = parts.Count(p => !p.Contains('='));
        // A call may leave off any of the defaulted parameters at the end.
        for (int count = required; count <= names.Length; count++)
        {
            string[] these = names[..count];
            if (_table.TryGetValue((name, count), out string[]? known) && (known is null || !known.SequenceEqual(these)))
                _table[(name, count)] = null;
            else
                _table[(name, count)] = these;
        }
    }

    /// <summary>A parameter's name: the last word before any default, past attributes and the type.</summary>
    private static string NameOf(string parameter)
    {
        string head = parameter.Split('=')[0].Trim();
        Match last = Regex.Match(head, @"(\w+)\s*$");
        return last.Success ? last.Groups[1].Value : head;
    }

    /// <summary>The arguments of the call whose '(' is at <paramref name="open"/>, or null if it doesn't close on this line.</summary>
    private static List<(int Start, string Text)>? Arguments(string code, int open)
    {
        int depth = 0;
        for (int i = open; i < code.Length; i++)
        {
            if (code[i] is '(' or '[' or '{')
                depth++;
            else if (code[i] is ')' or ']' or '}' && --depth == 0)
                return SplitTopLevel(code, open + 1, i).Where(arg => arg.Text.Length > 0).ToList();
        }

        return null;
    }

    /// <summary>Splits code[from..to) at commas outside brackets, giving each piece's first non-space column and trimmed text.</summary>
    private static List<(int Start, string Text)> SplitTopLevel(string code, int from, int to)
    {
        var pieces = new List<(int, string)>();
        int depth = 0, start = from;
        for (int i = from; i <= to; i++)
        {
            if (i == to || (code[i] == ',' && depth == 0))
            {
                string piece = code[start..i];
                int lead = piece.Length - piece.TrimStart().Length;
                pieces.Add((start + lead, piece.Trim()));
                start = i + 1;
            }
            else if (code[i] is '(' or '[' or '{' or '<')
                depth++;
            else if (code[i] is ')' or ']' or '}' or '>')
                depth = Math.Max(0, depth - 1);
        }

        return pieces;
    }

    private static string WithoutComments(string source) =>
        Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline), @"//[^\n]*", "");
}
