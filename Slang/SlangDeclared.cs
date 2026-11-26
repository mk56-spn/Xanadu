using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// The names the code being edited declares for itself, for their colour: its types (<c>struct Death</c>,
/// enums, interfaces, typedefs) and its globals, everything declared outside a body that isn't a function
/// (<c>static const uint CAP</c>, <c>uniform float zoom</c>, <c>Events&lt;Death&gt; deaths</c>).
/// Forgiving like <see cref="SlangSyntax"/>: what it can't read is simply left out.
/// </summary>
public sealed partial class SlangDeclared
{
    public static readonly SlangDeclared None = new();

    public HashSet<string> Types { get; } = [];
    public HashSet<string> Globals { get; } = [];

    [GeneratedRegex(@"\b(?:struct|enum|interface)\s+([A-Za-z_]\w*)")]
    private static partial Regex NamedType();

    [GeneratedRegex(@"\btypedef\b[^;]*?\b([A-Za-z_]\w*)\s*;|\btypealias\s+([A-Za-z_]\w*)")]
    private static partial Regex Alias();

    /// <summary>Attributes in front of a declaration (<c>[Size(CAP)]</c>), and an array size after its name.</summary>
    [GeneratedRegex(@"\[[^\[\]]*(?:\[[^\[\]]*\][^\[\]]*)*\]")]
    private static partial Regex Brackets();

    [GeneratedRegex(@"([A-Za-z_]\w*)\s*$")]
    private static partial Regex LastName();

    private static readonly HashSet<string> NotDeclarations = ["import", "typedef", "typealias", "struct", "enum", "interface", "return", "using", "module"];

    public static SlangDeclared Find(IReadOnlyList<string> lines)
    {
        var found = new SlangDeclared();
        // Comments become spaces, and strings with them: nothing in them declares anything.
        string source = Regex.Replace(string.Join('\n', lines), @"/\*.*?\*/|//[^\n]*|""(?:\\.|[^""\\])*""", m => new string(' ', m.Length), RegexOptions.Singleline);

        foreach (Match match in NamedType().Matches(source))
            found.Types.Add(match.Groups[1].Value);
        foreach (Match match in Alias().Matches(source))
            found.Types.Add(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);

        // Each statement straight at the top: what runs from the last ';', '{' or '}' to a ';'.
        int depth = 0, start = 0;
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (c == '{')
            {
                depth++;
                start = i + 1;
            }
            else if (c == '}')
            {
                depth = Math.Max(0, depth - 1);
                start = i + 1;
            }
            else if (c == ';')
            {
                if (depth == 0)
                    found.Global(source[start..i]);
                start = i + 1;
            }
        }

        return found;
    }

    private void Global(string statement)
    {
        string declared = Brackets().Replace(statement.Split('=')[0], " ").Trim();
        int space = declared.IndexOfAny([' ', '\t', '\n']);
        if (space < 0 || declared.Contains('(') || NotDeclarations.Contains(declared[..space]))
            return;
        if (LastName().Match(declared) is { Success: true } name)
            Globals.Add(name.Groups[1].Value);
    }
}
