using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

public sealed partial class SlangCompletions
{
    /// <summary>
    /// What the name at a column is, for hovering: every declaration of it (overloads each), or null
    /// for no name or one nothing is known about. After a dot it is a member, found the way completion
    /// finds the scope; otherwise the code's, the host's, the library's or an intrinsic.
    /// <paramref name="start"/> is where the name begins.
    /// </summary>
    public List<Completion>? Describe(string line, int column, IReadOnlyList<string> code, out int start)
    {
        static bool Word(char c) => char.IsLetterOrDigit(c) || c == '_';
        Learn(code);
        start = column;
        if (column < 0 || column >= line.Length || !Word(line[column]))
            return null;
        while (start > 0 && Word(line[start - 1]))
            start--;
        int end = column;
        while (end < line.Length && Word(line[end]))
            end++;
        string name = line[start..end];
        int comment = line.IndexOf("//", StringComparison.Ordinal);
        if (char.IsDigit(name[0]) || (comment >= 0 && comment < start))
            return null;

        string before = line[..start];
        if (AfterDot().IsMatch(before))
        {
            before = Collapsed(before);
            Match match = DotChain().Match(before);
            if (!match.Success || (match.Index > 0 && (char.IsDigit(before[match.Index - 1]) || before[match.Index - 1] == '.')))
                return null;
            string[] chain = match.Groups[1].Value.Split('.').Select(part => part.Trim()).ToArray();
            if (Scope(chain, code) is not { } scope)
                return null;
            if (_declared.TryGetValue((scope, name), out var own) || _code.Declared.TryGetValue((scope, name), out own))
                return own;
            foreach (string face in Faces(scope))
            {
                if (_declared.TryGetValue((face, name), out var shared))
                    return shared.ConvertAll(member => AsMemberOf(member, face, scope));
            }

            return null;
        }

        foreach (Completion own in CodeNames(code))
        {
            if (own.Name == name)
                return [WithMembers(own, own.Kind == CompletionKind.Type ? name : VariableType(name, code))];
        }

        if (_provided.Find(p => p.Name == name) is { Name: not null } provided)
            return [provided];
        if (_declared.TryGetValue(("", name), out var global))
            return LibraryType(name) is { } type ? [.. global.SkipLast(1), WithMembers(global[^1], type)] : global;
        foreach ((string intrinsic, string detail, string doc) in Intrinsics)
        {
            if (intrinsic == name)
                return [new Completion(name, CompletionKind.Function, detail, Doc: doc)];
        }

        return null;
    }

    /// <summary>A struct or interface, the library's or the code's, named by <paramref name="name"/> (alias resolved), or null for anything else.</summary>
    private string? LibraryType(string name)
    {
        string type = _aliases.GetValueOrDefault(name, name);
        return !_namespaces.Contains(type) && HasScope(type) ? type : null;
    }

    /// <summary>
    /// <paramref name="entry"/> with what <paramref name="type"/> has listed under its comment, straight
    /// from the library's source: every field, property and method as a code line, then the names it
    /// shares through its interfaces. Unchanged when the type is not the library's or has nothing.
    /// </summary>
    private Completion WithMembers(Completion entry, string? type)
    {
        if (type is null || LibraryType(type) is not { } scope || !TryScope(scope, out var own))
            return entry;

        var lines = new List<string>();
        foreach (Completion member in own)
            lines.Add("    " + member.Header);

        foreach (string face in Faces(scope))
        {
            if (_scopes.TryGetValue(face, out var shared) && shared.Count > 0)
            {
                IEnumerable<string> names = shared.Select(m => m.Name).Where(n => own.TrueForAll(o => o.Name != n)).Distinct();
                lines.Add("Also every " + face + "'s: " + string.Join(", ", names) + ".");
            }
        }

        if (lines.Count == 0)
            return entry;
        string list = "Has:\n" + string.Join('\n', lines);
        return entry with { Doc = entry.Doc is { Length: > 0 } doc ? doc + "\n\n" + list : list };
    }
}
