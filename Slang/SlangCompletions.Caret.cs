using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// Where the caret is, worked out once before any rule runs: after a dot (and
/// the scope the chain ends in), typing a word, giving a new name, and whether
/// a value starts there and what type it wants: the declared type of what is
/// being assigned, the uniform a <c>[Default(</c> belongs to, or the parameter
/// of the call being written (<c>c.paint(|</c> wants a float, an IShape, an
/// IGradient or a Box).
/// </summary>
public sealed partial class SlangCompletions
{
    private enum Where
    {
        /// <summary>Nothing fits: a comment, a dot after a number, an unknown chain.</summary>
        Nowhere,

        /// <summary>Just after a dot: a member of <see cref="Caret.Scope"/>.</summary>
        Member,

        /// <summary>Typing a word, or where a value starts with nothing typed yet.</summary>
        Word,
    }

    private sealed class Caret
    {
        public Where Where { get; init; }

        /// <summary>The column where what is typed begins.</summary>
        public int Start { get; init; }

        /// <summary>The word typed so far, perhaps empty.</summary>
        public string Typed { get; init; } = "";

        /// <summary>The line up to the caret.</summary>
        public string Before { get; init; } = "";

        /// <summary>The caret's line in the code, or -1 when not known.</summary>
        public int Line { get; init; } = -1;

        /// <summary>After a dot: the scope (namespace, struct, interface, enum) the chain ends in.</summary>
        public string? Scope { get; init; }

        /// <summary>A new name is being given (<c>float4 |</c>, <c>let |</c>): nothing can complete it.</summary>
        public bool NewName { get; init; }

        /// <summary>A value starts here (after <c>=</c>, <c>(</c>, <c>,</c>, <c>return</c>, an operator).</summary>
        public bool Value { get; init; }

        /// <summary>
        /// The types the value wants, as declared (<c>colour</c> stays colour), only those a rule can
        /// make: colour, the library's interfaces and structs, the code's structs, and what a family's
        /// <c>@fits</c> names (float). Empty when unknown.
        /// </summary>
        public IReadOnlyList<string> Wants { get; init; } = [];
    }

    private static readonly Caret NowhereCaret = new() { Where = Where.Nowhere };

    /// <summary>
    /// Where a value starts: after = (not ==), ( , ? : return, or an operator, with the start of a
    /// word or nothing typed yet.
    /// </summary>
    [GeneratedRegex(@"(?:(?<![=!<>])=|[(,?:+\-*/<>]|\breturn)\s*([A-Za-z_]\w*)?$")]
    private static partial Regex ValueStart();

    /// <summary>A function's declaration up to its open bracket: its parameters aren't values.</summary>
    [GeneratedRegex(@"^\s*(\w+)\s+\w+\s*\(")]
    private static partial Regex DeclarationOpen();

    /// <summary>What is assigned to, just before the =: a declaration (<c>colour c</c>) or a name or chain (<c>t.colour</c>).</summary>
    [GeneratedRegex(@"(?:\b([A-Za-z_]\w*)\s+([A-Za-z_]\w*)|(?<![\w.])([A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*))\s*$")]
    private static partial Regex AssignedTo();

    /// <summary>What is called, just before its open bracket: a name or a dotted chain.</summary>
    [GeneratedRegex(@"([A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*)\s*$")]
    private static partial Regex Callee();

    private Caret Locate(string beforeCaret, IReadOnlyList<string> code, int caretLine)
    {
        if (beforeCaret.Contains("//", StringComparison.Ordinal))
            return NowhereCaret;

        if (AfterDot().IsMatch(beforeCaret))
        {
            string collapsed = Collapsed(beforeCaret);
            Match match = DotChain().Match(collapsed);
            // A dot after a number (1.5) or a call isn't a member access.
            if (!match.Success || (match.Index > 0 && (char.IsDigit(collapsed[match.Index - 1]) || collapsed[match.Index - 1] == '.')))
                return NowhereCaret;
            string[] chain = match.Groups[1].Value.Split('.').Select(part => part.Trim()).ToArray();
            if (Scope(chain, code) is not { } scope)
                return NowhereCaret;
            string member = match.Groups[2].Value;
            return new Caret
            {
                Where = Where.Member, Start = beforeCaret.Length - member.Length, Typed = member,
                Before = beforeCaret, Line = caretLine, Scope = scope,
            };
        }

        Match word = WordAtEnd().Match(beforeCaret);
        string typed = word.Success ? word.Groups[1].Value : "";
        int start = word.Success ? word.Index : beforeCaret.Length;
        Match before = WordBeforeSpace().Match(beforeCaret[..start]);
        bool newName = word.Success && before.Success
                                    && (IsTypeName(before.Groups[1].Value)
                                        || before.Groups[1].Value is "let" or "var" or "struct" or "namespace" or "module" or "import");
        bool value = ValueAt(beforeCaret, code, caretLine, start, out List<string> wants);
        return new Caret
        {
            Where = Where.Word, Start = start, Typed = typed, Before = beforeCaret, Line = caretLine,
            NewName = newName, Value = value, Wants = wants,
        };
    }

    /// <summary>Whether a value starts at the caret, and the types it wants (see <see cref="Caret.Wants"/>).</summary>
    private bool ValueAt(string beforeCaret, IReadOnlyList<string> code, int caretLine, int start, out List<string> wants)
    {
        wants = [];
        Match match = ValueStart().Match(beforeCaret);
        if (!match.Success)
            return false;
        // Inside a declaration's parameter list (float4 shade(|) is not a value.
        Match declaration = DeclarationOpen().Match(beforeCaret);
        if (declaration.Success && (IsTypeName(declaration.Groups[1].Value) || declaration.Groups[1].Value == "void")
            && beforeCaret[declaration.Length..].Count(c => c == '(') >= beforeCaret[declaration.Length..].Count(c => c == ')'))
            return false;

        IEnumerable<string> types = beforeCaret[match.Index] switch
        {
            '=' => AssignedType(beforeCaret[..match.Index], code) is { } type ? [type] : [],
            '(' or ',' => ArgumentTypes(beforeCaret[..start], code, caretLine),
            _ => [],
        };
        wants = types.Where(Makeable).Distinct().ToList();
        return true;
    }

    /// <summary>The declared type of what is assigned to just before an =, or null.</summary>
    private string? AssignedType(string head, IReadOnlyList<string> code)
    {
        Match target = AssignedTo().Match(head);
        if (!target.Success)
            return null;
        if (target.Groups[1].Success)
            return IsTypeName(target.Groups[1].Value) ? target.Groups[1].Value : null;

        string[] chain = target.Groups[3].Value.Split('.').Select(part => part.Trim()).ToArray();
        if (chain.Length == 1)
            return DeclaredType(chain[0], code);
        return Scope(chain[..^1], code) is { } owner && TryMemberTypes(owner, out var types)
            ? types.GetValueOrDefault(chain[^1])
            : null;
    }

    /// <summary>The type a name is declared with in the code, as written (<c>colour c</c> → colour), or null.</summary>
    private string? DeclaredType(string name, IReadOnlyList<string> code)
    {
        foreach (string line in code)
        {
            foreach (Match match in TypedName().Matches(line))
            {
                if (match.Groups[2].Value == name && IsTypeName(match.Groups[1].Value))
                    return match.Groups[1].Value;
            }
        }

        return null;
    }

    /// <summary>
    /// What the argument being written wants: the parameter at its place in every overload of the
    /// call it is in (generic ones as their constraint), or a <c>[Default(</c>'s uniform type.
    /// Nothing for a call on a call's result, or one nothing is known about.
    /// </summary>
    private IEnumerable<string> ArgumentTypes(string text, IReadOnlyList<string> code, int caretLine)
    {
        int depth = 0, index = 0, open = -1;
        for (int i = text.Length - 1; i >= 0 && open < 0; i--)
        {
            char c = text[i];
            if (c is ')' or ']')
                depth++;
            else if (c is '(' or '[' && depth > 0)
                depth--;
            else if (c == '(')
                open = i;
            else if (c == ',' && depth == 0)
                index++;
        }

        if (open < 0)
            return [];
        string callee = Collapsed(text[..open]);
        if (Regex.IsMatch(callee, @"\[\s*Default\s*$"))
        {
            string line = caretLine >= 0 && caretLine < code.Count ? code[caretLine] : text;
            Match uniform = Regex.Match(line, @"\buniform\s+(\w+)");
            return uniform.Success ? [uniform.Groups[1].Value] : [];
        }

        Match called = Callee().Match(callee);
        if (!called.Success || (called.Index > 0 && callee[called.Index - 1] is '.' or ')' or ']'))
            return [];
        string[] chain = called.Groups[1].Value.Split('.').Select(part => part.Trim()).ToArray();
        string name = chain[^1];
        var scopes = new List<string>();
        if (chain.Length == 1)
            scopes.Add("");
        else if (Scope(chain[..^1], code) is { } owner)
            scopes.AddRange([owner, .. Faces(owner)]);

        var wanted = new List<string>();
        foreach (string scope in scopes)
        {
            foreach (string[] overload in _parameters.GetValueOrDefault((scope, name)) ?? [])
            {
                if (index < overload.Length)
                    wanted.Add(overload[index]);
            }
        }

        return wanted;
    }

    /// <summary>Whether a rule can offer something that makes the type: a colour, a library interface or struct, a code struct, or a family that fits it.</summary>
    private bool Makeable(string type) =>
        type == "colour" || _interfaceNames.Contains(type) || _fits.Values.Any(fits => fits.Contains(type)) || _code.Scopes.ContainsKey(type)
        || _globals.Exists(g => g.Kind == CompletionKind.Type && g.Name == type && !_aliases.ContainsKey(type));
}
