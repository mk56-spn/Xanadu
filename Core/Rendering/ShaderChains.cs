using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Xanadu.Rendering;

/// <summary>
/// Lets an effect chain the library's in-place methods. Slang has no
/// references, so a <c>[mutating]</c> method can't hand back the value it
/// changed: <c>c.move(a).rotate(b)</c> and <c>Circle(30).move(5)</c> don't
/// compile. Each such method has a copying twin (<c>move</c> and <c>moved</c>,
/// <c>paint</c> and <c>painted</c>), so before Slang sees an effect body two
/// rewrites turn a chain into code that means the same:
/// <list type="number">
/// <item>An in-place call on a temporary (a call's result, a new shape) takes
/// the copying twin: <c>Circle(30).move(5).rotate(t)</c> →
/// <c>Circle(30).moved(5).rotated(t)</c>.</item>
/// <item>A statement that starts with a variable and chains in-place calls
/// becomes an assignment of the copying chain: <c>c.move(a).rotate(b);</c> →
/// <c>c = c.moved(a).rotated(b);</c>.</item>
/// </list>
/// A single in-place call on a variable (<c>c.move(a);</c>) is left alone.
/// The rewrites only add text inside a line, so error lines stay the author's
/// (columns after a rewrite on that line move a little). The method pairs come
/// from the library itself: every <c>[mutating]</c> method with a public twin
/// named with -d or -ed. The host loads them at start (<see cref="Use"/>).
/// </summary>
public sealed partial class ShaderChains
{
    /// <summary>In-place method → its copying twin.</summary>
    private readonly Dictionary<string, string> _twins = new(StringComparer.Ordinal);

    /// <summary>The running host's pairs; empty where nothing loaded them (tests), which rewrites nothing.</summary>
    public static ShaderChains Library { get; private set; } = new();

    public static void Use(ShaderChains chains) => Library = chains ?? throw new ArgumentNullException(nameof(chains));

    public IReadOnlyDictionary<string, string> Twins => _twins;

    [GeneratedRegex(@"\[\s*mutating\s*\]\s*public\s+void\s+(\w+)\s*(?:<[^>()]*>)?\s*\(")]
    private static partial Regex Mutating();

    [GeneratedRegex(@"public\s+[\w<>, ]+?\s+(\w+)\s*(?:<[^>()]*>)?\s*\(")]
    private static partial Regex Method();

    [GeneratedRegex(@"\.\s*([A-Za-z_]\w*)\s*\(")]
    private static partial Regex DotCall();

    /// <summary>The in-place methods with copying twins in the given library modules.</summary>
    public static ShaderChains FromSources(IEnumerable<string> sources)
    {
        var chains = new ShaderChains();
        string[] all = sources.ToArray();
        var mutating = new HashSet<string>(StringComparer.Ordinal);
        var methods = new HashSet<string>(StringComparer.Ordinal);
        foreach (string source in all)
        {
            foreach (Match match in Mutating().Matches(source))
                mutating.Add(match.Groups[1].Value);
            foreach (Match match in Method().Matches(source))
                methods.Add(match.Groups[1].Value);
        }

        foreach (string name in mutating)
        {
            string twin = name + (name.EndsWith('e') ? "d" : "ed");
            if (methods.Contains(twin))
                chains._twins[name] = twin;
        }

        return chains;
    }

    /// <summary>The effect body with chains of in-place calls rewritten (see the class summary).</summary>
    public string Rewrite(string source)
    {
        if (_twins.Count == 0)
            return source;

        // Scan a copy with comments and strings blanked, so they're never touched; places don't move.
        string code = Blank(source);
        var inserts = new List<(int At, string Text)>();
        foreach (Match call in DotCall().Matches(code))
        {
            string name = call.Groups[1].Value;
            if (!_twins.TryGetValue(name, out string? twin))
                continue;
            int dot = call.Index;
            int nameEnd = call.Groups[1].Index + name.Length;
            int receiverEnd = SkipSpaceBack(code, dot - 1);
            if (receiverEnd < 0)
                continue;

            if (code[receiverEnd] == ')')
            {
                // Rule 1: on a temporary, the copying twin.
                inserts.Add((nameEnd, twin[name.Length..]));
                continue;
            }

            // Rule 2: a statement that starts with this variable and chains on from this call.
            int receiverStart = ReceiverStart(code, receiverEnd);
            if (receiverStart < 0 || !StartsStatement(code, receiverStart))
                continue;
            int close = Close(code, call.Index + call.Length - 1);
            int next = close < 0 ? code.Length : SkipSpace(code, close + 1);
            if (next >= code.Length || code[next] != '.' || !AllInPlace(code, next))
                continue;

            string receiver = source[receiverStart..(receiverEnd + 1)];
            inserts.Add((receiverStart, receiver + " = "));
            inserts.Add((nameEnd, twin[name.Length..]));
        }

        if (inserts.Count == 0)
            return source;

        var rewritten = new StringBuilder(source);
        foreach ((int at, string text) in inserts.OrderByDescending(insert => insert.At))
            rewritten.Insert(at, text);
        return rewritten.ToString();
    }

    /// <summary>Whether the chain from <paramref name="at"/> (a dot) to the statement's end is only in-place calls.</summary>
    private bool AllInPlace(string code, int at)
    {
        while (true)
        {
            Match call = DotCall().Match(code, at);
            if (!call.Success || call.Index != at || !_twins.ContainsKey(call.Groups[1].Value))
                return false;
            int close = Close(code, call.Index + call.Length - 1);
            if (close < 0)
                return false;
            at = SkipSpace(code, close + 1);
            if (at < code.Length && code[at] == ';')
                return true;
            if (at >= code.Length || code[at] != '.')
                return false;
        }
    }

    /// <summary>
    /// Where a variable path ending at <paramref name="end"/> starts (<c>c</c>,
    /// <c>a.b</c>, <c>shapes[i]</c>); -1 if it isn't one.
    /// </summary>
    private static int ReceiverStart(string code, int end)
    {
        int i = end;
        while (true)
        {
            if (i >= 0 && code[i] == ']')
            {
                int depth = 0;
                for (; i >= 0; i--)
                {
                    if (code[i] == ']')
                        depth++;
                    else if (code[i] == '[' && --depth == 0)
                        break;
                }

                i = SkipSpaceBack(code, i - 1);
            }

            if (i < 0 || !IsWord(code[i]))
                return -1;
            while (i > 0 && IsWord(code[i - 1]))
                i--;
            if (char.IsDigit(code[i]))
                return -1;

            int before = SkipSpaceBack(code, i - 1);
            if (before >= 0 && code[before] == '.')
            {
                i = SkipSpaceBack(code, before - 1);
                if (i < 0 || code[i] == ')')
                    return -1;
                continue;
            }

            return i;
        }
    }

    /// <summary>Whether a statement starts here: after ; { } or else, or after the ) of an if, for or while header.</summary>
    private static bool StartsStatement(string code, int at)
    {
        int before = SkipSpaceBack(code, at - 1);
        if (before < 0 || code[before] is ';' or '{' or '}')
            return true;
        if (before >= 3 && code.AsSpan(before - 3, 4).SequenceEqual("else") && (before < 4 || !IsWord(code[before - 4])))
            return true;
        if (code[before] != ')')
            return false;

        int depth = 0;
        int open = before;
        for (; open >= 0; open--)
        {
            if (code[open] == ')')
                depth++;
            else if (code[open] == '(' && --depth == 0)
                break;
        }

        int keywordEnd = SkipSpaceBack(code, open - 1);
        if (keywordEnd < 0)
            return false;
        int keywordStart = keywordEnd;
        while (keywordStart > 0 && IsWord(code[keywordStart - 1]))
            keywordStart--;
        return code[keywordStart..(keywordEnd + 1)] is "if" or "for" or "while";
    }

    private static int Close(string code, int open)
    {
        int depth = 0;
        for (int i = open; i < code.Length; i++)
        {
            if (code[i] == '(')
                depth++;
            else if (code[i] == ')' && --depth == 0)
                return i;
        }

        return -1;
    }

    private static int SkipSpaceBack(string code, int i)
    {
        while (i >= 0 && char.IsWhiteSpace(code[i]))
            i--;
        return i;
    }

    private static int SkipSpace(string code, int i)
    {
        while (i < code.Length && char.IsWhiteSpace(code[i]))
            i++;
        return i;
    }

    private static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static string Blank(string source) =>
        Regex.Replace(source, @"/\*.*?\*/|//[^\n]*|""(?:[^""\\\n]|\\.)*""", m => Regex.Replace(m.Value, @"[^\n]", " "), RegexOptions.Singleline);
}
