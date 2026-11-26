using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// The names the code being edited declares for itself, for their colour: its types (<c>struct Death</c>,
/// enums, interfaces, typedefs) and its globals, everything declared outside a body that isn't a function
/// (<c>static const uint CAP</c>, <c>uniform float zoom</c>, <c>Events&lt;Death&gt; deaths</c>).
/// A global is marked only where it means the global (<see cref="SlangRename"/>'s places of it), so a field or
/// local of the same name (<c>e.spread</c>, <c>float spread;</c> in a struct) stays what it is. A global of the
/// stack's other files is marked where this file doesn't declare that name itself.
/// Forgiving like <see cref="SlangSyntax"/>: what it can't read is simply left out.
/// </summary>
public sealed partial class SlangDeclared
{
    public static readonly SlangDeclared None = new();

    public HashSet<string> Types { get; } = [];

    /// <summary>The globals' names, for the stack's other files.</summary>
    public HashSet<string> Globals { get; } = [];

    /// <summary>Where (line, column) a name means one of the globals.</summary>
    private readonly HashSet<(int Line, int Column)> _globalPlaces = [];

    [GeneratedRegex(@"\b(?:struct|enum|interface)\s+([A-Za-z_]\w*)")]
    private static partial Regex NamedType();

    [GeneratedRegex(@"\btypedef\b[^;]*?\b([A-Za-z_]\w*)\s*;|\btypealias\s+([A-Za-z_]\w*)")]
    private static partial Regex Alias();

    /// <summary>Attributes in front of a declaration (<c>[Size(CAP)]</c>), and an array size after its name.</summary>
    [GeneratedRegex(@"\[[^\[\]]*(?:\[[^\[\]]*\][^\[\]]*)*\]")]
    private static partial Regex Brackets();

    [GeneratedRegex(@"([A-Za-z_]\w*)\s*$")]
    private static partial Regex LastName();

    [GeneratedRegex(@"[A-Za-z_]\w*")]
    private static partial Regex Word();

    private static readonly HashSet<string> NotDeclarations = ["import", "typedef", "typealias", "struct", "enum", "interface", "return", "using", "module"];

    /// <param name="elsewhere">The stack's other files, whose globals this one may use.</param>
    public static SlangDeclared Find(IReadOnlyList<string> lines, IEnumerable<string>? elsewhere = null)
    {
        string text = string.Join('\n', lines);
        // Comments and strings become spaces of the same length, so offsets stay the original's.
        string source = Regex.Replace(text, @"/\*.*?\*/|//[^\n]*|""(?:\\.|[^""\\])*""", m => Regex.Replace(m.Value, @"[^\n]", " "), RegexOptions.Singleline);
        var found = new SlangDeclared();

        foreach (Match match in NamedType().Matches(source))
            found.Types.Add(match.Groups[1].Value);
        foreach (Match match in Alias().Matches(source))
            found.Types.Add(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);

        // Each statement straight at the top: what runs from the last ';', '{' or '}' to a ';'.
        var declarations = new List<int>();
        int depth = 0, start = 0;
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (c is '{' or '}' or ';')
            {
                if (c == ';' && depth == 0 && Global(source[start..i]) is { } name)
                {
                    found.Globals.Add(source.Substring(start + name, Word().Match(source, start + name).Length));
                    declarations.Add(start + name);
                }

                depth = c == '{' ? depth + 1 : c == '}' ? Math.Max(0, depth - 1) : depth;
                start = i + 1;
            }
        }

        foreach (int at in declarations)
        {
            (int line, int column) = Place(text, at);
            foreach ((int Line, int Column) place in SlangRename.Find(lines, line, column)?.Places ?? [])
                found._globalPlaces.Add(place);
        }

        if (elsewhere is not null)
            found.MarkElsewhere(lines, source, text, elsewhere);
        return found;
    }

    /// <summary>
    /// The other files' globals: a name of one is theirs wherever this file doesn't declare that name in scope
    /// (no field, local or global of its own), and it isn't a member after a dot.
    /// </summary>
    private void MarkElsewhere(IReadOnlyList<string> lines, string source, string text, IEnumerable<string> elsewhere)
    {
        var theirs = new HashSet<string>(StringComparer.Ordinal);
        foreach (string other in elsewhere)
            theirs.UnionWith(Find(other.Split('\n')).Globals);
        theirs.ExceptWith(Globals);
        if (theirs.Count == 0)
            return;

        foreach (Match word in Word().Matches(source))
        {
            if (!theirs.Contains(word.Value) || Before(source, word.Index) == '.')
                continue;
            (int line, int column) = Place(text, word.Index);
            if (SlangRename.Find(lines, line, column) is null)
                _globalPlaces.Add((line, column));
        }
    }

    /// <summary>A line's tokens with each name that means a global marked <see cref="TokenKind.Global"/> (over a namespace of the same name: <c>Field field; field.spread</c>).</summary>
    public List<Token> Mark(int line, List<Token> tokens)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind is TokenKind.Plain or TokenKind.Namespace && _globalPlaces.Contains((line, tokens[i].Start)))
                tokens[i] = tokens[i] with { Kind = TokenKind.Global };
        }

        return tokens;
    }

    /// <summary>Where a top-level statement declares a global (the name's offset in it), or null when it doesn't.</summary>
    private static int? Global(string statement)
    {
        // Attributes and array sizes become spaces, and a value is cut off, so the name's offset stays the statement's.
        string declared = Brackets().Replace(statement.Split('=')[0], m => new string(' ', m.Length));
        string trimmed = declared.Trim();
        int space = trimmed.IndexOfAny([' ', '\t', '\n']);
        if (space < 0 || trimmed.Contains('(') || NotDeclarations.Contains(trimmed[..space]))
            return null;
        return LastName().Match(declared) is { Success: true } name ? name.Groups[1].Index : null;
    }

    private static char Before(string source, int at)
    {
        while (at > 0 && char.IsWhiteSpace(source[at - 1]))
            at--;
        return at > 0 ? source[at - 1] : ' ';
    }

    private static (int Line, int Column) Place(string text, int offset)
    {
        int lineStart = text.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1;
        if (offset == 0)
            lineStart = 0;
        int line = 0;
        for (int i = 0; i < lineStart; i++)
            line += text[i] == '\n' ? 1 : 0;
        return (line, offset - lineStart);
    }
}
