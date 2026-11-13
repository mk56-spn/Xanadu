using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>A place in one of the library's modules: its file, and the line and column of the name there.</summary>
public readonly record struct LibraryPlace(string Path, int Line, int Column);

/// <summary>
/// Where each public name of the library is declared, for going to it from
/// code that uses it: functions, structs, namespaces, aliases, constants,
/// fields and properties, each with the namespace or struct it is inside, and
/// each module by its own name (<c>import xanadu.ease</c>). A name used after a
/// dot (<c>ease.outBounce</c>) prefers the one inside what is before the dot.
/// </summary>
public sealed partial class SlangLibraryDeclarations
{
    private readonly Dictionary<string, List<(string? Scope, LibraryPlace Place)>> _names = new(StringComparer.Ordinal);

    [GeneratedRegex(@"/\*.*?\*/|//[^\n]*|""(?:[^""\\\n]|\\.)*""", RegexOptions.Singleline)]
    private static partial Regex CommentsAndStrings();

    /// <summary>A public declaration: the last name before its <c>(</c>, <c>{</c>, <c>;</c>, <c>=</c> or <c>:</c> (generics skipped).</summary>
    [GeneratedRegex(@"\bpublic\b[^;{}()=:]*?\b([A-Za-z_]\w*)\s*(?:<[^<>;{}()]*>)?\s*[({;=:]")]
    private static partial Regex PublicDeclaration();

    /// <summary>What opens a named scope: <c>namespace ease {</c>, <c>struct Clip : IThing {</c>.</summary>
    [GeneratedRegex(@"\b(?:namespace|struct|interface|enum|extension)\s+([A-Za-z_]\w*)[^;{}]*$")]
    private static partial Regex ScopeOpener();

    /// <param name="modules">Each module's path and text.</param>
    public static SlangLibraryDeclarations FromModules(IEnumerable<(string Path, string Text)> modules)
    {
        var found = new SlangLibraryDeclarations();
        foreach ((string path, string text) in modules)
            found.Add(path, text);
        return found;
    }

    private void Add(string path, string text)
    {
        // Each module by its own name, at its top, so `xanadu.ease` goes to the file.
        Note(Path.GetFileNameWithoutExtension(path), "xanadu", new LibraryPlace(path, 0, 0));

        string code = CommentsAndStrings().Replace(text, m => Regex.Replace(m.Value, @"[^\n]", " "));
        List<(int Open, int End, string? Name)> scopes = Scopes(code);
        foreach (Match match in PublicDeclaration().Matches(code))
        {
            Group name = match.Groups[1];
            if (name.Value is "__init" or "__subscript")
                continue;
            (int line, int column) = Place(code, name.Index);
            var place = new LibraryPlace(path, line, column);
            Note(name.Value, ScopeOf(scopes, name.Index), place);
            // An attribute's struct is used by its short name: DefaultAttribute is [Default].
            if (name.Value.Length > "Attribute".Length && name.Value.EndsWith("Attribute", StringComparison.Ordinal))
                Note(name.Value[..^"Attribute".Length], ScopeOf(scopes, name.Index), place);
        }
    }

    private void Note(string name, string? scope, LibraryPlace place)
    {
        if (!_names.TryGetValue(name, out var places))
            _names[name] = places = [];
        places.Add((scope, place));
    }

    /// <summary>
    /// Where the name at a column of a line is declared, or null when the library doesn't declare it.
    /// After a dot the name inside what is before the dot wins; otherwise the first declared at the top.
    /// </summary>
    public LibraryPlace? Find(string line, int column)
    {
        static bool Word(char c) => char.IsLetterOrDigit(c) || c == '_';
        column = Math.Clamp(column, 0, line.Length);
        int start = column, end = column;
        while (start > 0 && Word(line[start - 1]))
            start--;
        while (end < line.Length && Word(line[end]))
            end++;
        if (start == end || char.IsDigit(line[start]) || !_names.TryGetValue(line[start..end], out var places))
            return null;

        int dot = start - 1;
        while (dot >= 0 && line[dot] == ' ')
            dot--;
        string? qualifier = null;
        if (dot >= 0 && line[dot] == '.')
        {
            int qualifierEnd = dot;
            while (qualifierEnd > 0 && line[qualifierEnd - 1] == ' ')
                qualifierEnd--;
            int qualifierStart = qualifierEnd;
            while (qualifierStart > 0 && Word(line[qualifierStart - 1]))
                qualifierStart--;
            qualifier = line[qualifierStart..qualifierEnd];
        }

        foreach ((string? scope, LibraryPlace place) in places)
        {
            if (qualifier is not null && scope == qualifier)
                return place;
        }

        foreach ((string? scope, LibraryPlace place) in places)
        {
            if (scope is null)
                return place;
        }

        return places[0].Place;
    }

    /// <summary>Every {…} in the code, with the name of the namespace or struct it opens (null for a function's body).</summary>
    private static List<(int Open, int End, string? Name)> Scopes(string code)
    {
        var scopes = new List<(int Open, int End, string? Name)>();
        var open = new Stack<(int Open, string? Name)>();
        int region = 0;
        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];
            if (c == '{')
            {
                Match opener = ScopeOpener().Match(code[region..i]);
                open.Push((i, opener.Success ? opener.Groups[1].Value : null));
                region = i + 1;
            }
            else if (c == '}')
            {
                if (open.Count > 0)
                {
                    (int start, string? name) = open.Pop();
                    scopes.Add((start, i, name));
                }

                region = i + 1;
            }
            else if (c == ';')
                region = i + 1;
        }

        while (open.Count > 0)
        {
            (int start, string? name) = open.Pop();
            scopes.Add((start, code.Length, name));
        }

        return scopes;
    }

    /// <summary>The innermost named scope round an offset; null at the top or only inside function bodies.</summary>
    private static string? ScopeOf(List<(int Open, int End, string? Name)> scopes, int index)
    {
        string? name = null;
        int innermost = -1;
        foreach ((int open, int end, string? scope) in scopes)
        {
            if (scope is not null && open < index && index < end && open > innermost)
            {
                innermost = open;
                name = scope;
            }
        }

        return name;
    }

    private static (int Line, int Column) Place(string code, int index)
    {
        int line = 0, start = 0;
        for (int i = 0; i < index; i++)
        {
            if (code[i] == '\n')
            {
                line++;
                start = i + 1;
            }
        }

        return (line, index - start);
    }
}
