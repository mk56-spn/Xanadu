using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>A float3 the compile took as a <c>colour</c> (alpha 1): where it starts in the author's source (0-based) and how long it is.</summary>
public readonly record struct ColourConversion(int Line, int Column, int Length);

/// <summary>
/// Lets a float3 stand where a <c>colour</c> is wanted, as if Slang converted it
/// implicitly (it can't: user code may not declare implicit conversions, and
/// <c>colour</c> is only a name for float4). Slang reports each such place as a
/// type mismatch; when the slot is written as a colour, the expression is wrapped
/// in <c>colour(…)</c> (alpha 1) and the body compiled again. Plain float4 slots
/// stay errors. The slots that count:
/// <list type="bullet">
/// <item><c>colour c = rgb;</c> and an assignment to a name declared as a colour
/// (variable, parameter or field, in the body or the library)</item>
/// <item><c>return rgb;</c> in a function that returns a colour</item>
/// <item>an argument for a parameter declared as a colour</item>
/// </list>
/// The editor shows each conversion as an inlay hint.
/// </summary>
public static partial class SlangColourConversions
{
    private const string Open = "colour(";
    private const string Close = ")";

    [GeneratedRegex(@"^(error|warning|note)\b")]
    private static partial Regex Header();

    [GeneratedRegex(@"^\s*-->\s*(.+):(\d+):(\d+)\s*$")]
    private static partial Regex Location();

    [GeneratedRegex(@"expected an expression of type 'vector<float,\s*4>', got 'vector<float,\s*3>'")]
    private static partial Regex Float3ForFloat4();

    /// <summary>A declared function (return type, name, the '(' that opens its parameters) or a constructor.</summary>
    [GeneratedRegex(@"\b(?:([A-Za-z_][\w:]*(?:<[^<>()]*>)?)\s+([A-Za-z_]\w*)|(__init))\s*(?:<[^<>()]*>)?\s*\(")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"\bstruct\s+(\w+)")]
    private static partial Regex Struct();

    /// <summary>A name declared as a colour: variable, parameter or field.</summary>
    [GeneratedRegex(@"\bcolour\s+([A-Za-z_]\w*)\s*(?=[;=,)\[:])")]
    private static partial Regex ColourName();

    private static readonly HashSet<string> NotTypes = new(StringComparer.Ordinal)
    {
        "return", "else", "new", "case", "if", "for", "while", "switch", "do", "import", "using", "typealias",
    };

    private static readonly HashSet<string> Modifiers = new(StringComparer.Ordinal)
    {
        "in", "out", "inout", "const", "uniform", "no_diff", "static", "public", "internal", "private",
    };

    /// <summary>The source with each conversion's expression wrapped, and for each of its characters the source index it came from (-1 for added text).</summary>
    public static string Apply(string source, IReadOnlyList<ColourConversion> conversions, out int[] origin)
    {
        var inserts = new List<(int At, string Text, int Order)>();
        int[] lineStarts = LineStarts(source);
        foreach (ColourConversion conversion in conversions)
        {
            int start = lineStarts[conversion.Line] + conversion.Column;
            // At one place a close goes before an open, so neighbours don't nest.
            inserts.Add((start, Open, 1));
            inserts.Add((start + conversion.Length, Close, 0));
        }

        inserts.Sort((a, b) => a.At != b.At ? a.At.CompareTo(b.At) : a.Order.CompareTo(b.Order));
        var text = new StringBuilder(source.Length + inserts.Count * 4);
        var from = new List<int>(source.Length + inserts.Count * 4);
        int next = 0;
        for (int i = 0; i <= source.Length; i++)
        {
            while (next < inserts.Count && inserts[next].At == i)
            {
                text.Append(inserts[next].Text);
                from.AddRange(Enumerable.Repeat(-1, inserts[next].Text.Length));
                next++;
            }

            if (i < source.Length)
            {
                text.Append(source[i]);
                from.Add(i);
            }
        }

        origin = [.. from];
        return text.ToString();
    }

    /// <summary>
    /// The new conversions Slang's output asks for, in <paramref name="source"/>'s places: each float3 given
    /// where a float4 was wanted, in the code labelled <paramref name="label"/>, whose slot is a colour.
    /// </summary>
    /// <param name="compiled">The code compiled: <paramref name="source"/> after <see cref="Apply"/>.</param>
    /// <param name="library">The library's text, for colour parameters and fields declared there.</param>
    public static List<ColourConversion> Find(string output, string label, string source, string compiled, int[] origin, string library)
    {
        var found = new List<ColourConversion>();
        string code = Blank(compiled);
        int[] compiledStarts = LineStarts(compiled);
        int[] sourceStarts = LineStarts(source);
        Slots? slots = null;
        foreach ((int line, int column) in Mismatches(output, label))
        {
            if (line < 1 || line > compiledStarts.Length)
                continue;
            int at = compiledStarts[line - 1] + column - 1;
            if (at < 0 || at >= code.Length)
                continue;
            if (Expression(code, at) is not (int start, int end, Slot slot))
                continue;
            slots ??= new Slots(Blank(library) + "\n" + code);
            if (!slots.IsColour(slot, code))
                continue;
            if (origin[start] < 0 || origin[end - 1] < 0)
                continue;

            int from = origin[start], to = origin[end - 1] + 1;
            int sourceLine = Array.FindLastIndex(sourceStarts, s => s <= from);
            var conversion = new ColourConversion(sourceLine, from - sourceStarts[sourceLine], to - from);
            if (!found.Contains(conversion))
                found.Add(conversion);
        }

        return found;
    }

    /// <summary>Where Slang says a float3 was given for a float4, in the labelled code (1-based).</summary>
    private static IEnumerable<(int Line, int Column)> Mismatches(string output, string label)
    {
        string[] lines = output.ReplaceLineEndings("\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].StartsWith("error", StringComparison.Ordinal))
                continue;
            (int Line, int Column)? place = null;
            bool mismatch = false;
            for (int j = i + 1; j < lines.Length && !Header().IsMatch(lines[j]); j++)
            {
                Match location = Location().Match(lines[j]);
                if (location.Success && location.Groups[1].Value.Trim() == label)
                    place = (int.Parse(location.Groups[2].Value), int.Parse(location.Groups[3].Value));
                mismatch |= Float3ForFloat4().IsMatch(lines[j]);
            }

            if (mismatch && place is { } found)
                yield return found;
        }
    }

    /// <summary>What an expression stands in for.</summary>
    private abstract record Slot;

    /// <summary><c>colour c = …</c>, <c>c = …</c>, <c>x.field = …</c>: the text before the <c>=</c>.</summary>
    private sealed record Assigned(int EqualsAt) : Slot;

    /// <summary><c>return …</c>, at this index.</summary>
    private sealed record Returned(int At) : Slot;

    /// <summary>Argument <paramref name="Index"/> of the call whose '(' is at <paramref name="Open"/>.</summary>
    private sealed record Argument(int Open, int Index) : Slot;

    /// <summary>
    /// The whole expression around <paramref name="at"/> (where Slang pointed: often an operator or a
    /// member inside it) up to the nearest assignment, return, argument separator or statement end;
    /// null where it ends some other way (a ternary branch, a compound assignment).
    /// </summary>
    private static (int Start, int End, Slot Slot)? Expression(string code, int at)
    {
        int depth = 0;
        int i = at - 1;
        Slot? slot = null;
        for (; i >= 0; i--)
        {
            char c = code[i];
            if (c is ')' or ']')
                depth++;
            else if (c is '(' or '[')
            {
                if (depth == 0)
                {
                    if (c == '[')
                        return null;
                    slot = new Argument(i, 0);
                    break;
                }

                depth--;
            }
            else if (depth > 0 || IsScope(code, i))
                continue;
            else if (c is ';' or '{' or '}' or '?' or ':')
                return null;
            else if (c == ',')
            {
                slot = ArgumentAround(code, i);
                if (slot is null)
                    return null;
                break;
            }
            else if (c == '=')
            {
                if (i + 1 < code.Length && code[i + 1] == '=')
                    return null;
                if (i > 0 && "=!<>+-*/%&|^".Contains(code[i - 1]))
                    return null;
                slot = new Assigned(i);
                break;
            }
            else if (IsWordAt(code, i, "return"))
            {
                slot = new Returned(i - "return".Length + 1);
                break;
            }
        }

        if (slot is null)
            return null;

        int start = i + 1;
        depth = 0;
        int end = at;
        for (; end < code.Length; end++)
        {
            char c = code[end];
            if (c is '(' or '[')
                depth++;
            else if (c is ')' or ']')
            {
                if (depth == 0)
                    break;
                depth--;
            }
            else if (depth == 0 && c is ',' or ';' or '}' or '{')
                break;
            else if (depth == 0 && c is '?' or ':' && !IsScope(code, end))
                return null;
        }

        while (start < end && char.IsWhiteSpace(code[start]))
            start++;
        while (end > start && char.IsWhiteSpace(code[end - 1]))
            end--;
        return start < end ? (start, end, slot) : null;
    }

    /// <summary>The argument slot for an expression just after the comma at <paramref name="comma"/>.</summary>
    private static Argument? ArgumentAround(string code, int comma)
    {
        int index = 1, depth = 0;
        for (int i = comma - 1; i >= 0; i--)
        {
            char c = code[i];
            if (c is ')' or ']' or '}')
                depth++;
            else if (c is '(' or '[' or '{')
            {
                if (depth == 0)
                    return c == '(' ? new Argument(i, index) : null;
                depth--;
            }
            else if (depth == 0 && c == ',')
                index++;
            else if (depth == 0 && c == ';')
                return null;
        }

        return null;
    }

    /// <summary>Whether <paramref name="word"/> ends at <paramref name="end"/>, as a whole word.</summary>
    private static bool IsWordAt(string code, int end, string word)
    {
        int start = end - word.Length + 1;
        if (start < 0 || string.CompareOrdinal(code, start, word, 0, word.Length) != 0)
            return false;
        return (start == 0 || !IsWordChar(code[start - 1])) && (end + 1 >= code.Length || !IsWordChar(code[end + 1]));
    }

    /// <summary>Whether the ':' at <paramref name="at"/> is half of a <c>::</c>.</summary>
    private static bool IsScope(string code, int at) =>
        code[at] == ':' && ((at > 0 && code[at - 1] == ':') || (at + 1 < code.Length && code[at + 1] == ':'));

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>What's declared as a colour, in the library and the body.</summary>
    private sealed class Slots
    {
        private readonly string _code;
        private readonly HashSet<string> _names = new(StringComparer.Ordinal);

        /// <summary>Whether each parameter is a colour, by (function, argument count); null where declarations disagree.</summary>
        private readonly Dictionary<(string Name, int Count), bool[]?> _parameters = [];

        public Slots(string code)
        {
            _code = code;
            foreach (Match name in ColourName().Matches(code))
                _names.Add(name.Groups[1].Value);

            var structs = Struct().Matches(code).Select(match => (match.Index, Name: match.Groups[1].Value)).ToList();
            foreach (Match declaration in Declaration().Matches(code))
            {
                string name;
                if (declaration.Groups[3].Success)
                {
                    string? owner = structs.LastOrDefault(s => s.Index < declaration.Index).Name;
                    if (owner is null)
                        continue;
                    name = owner;
                }
                else
                {
                    if (NotTypes.Contains(declaration.Groups[1].Value))
                        continue;
                    name = declaration.Groups[2].Value;
                }

                int open = declaration.Index + declaration.Length - 1;
                int close = Matching(code, open);
                if (close < 0)
                    continue;
                Add(name, Pieces(code, open + 1, close));
            }
        }

        public bool IsColour(Slot slot, string code) => slot switch
        {
            Assigned assigned => AssignsColour(code, assigned.EqualsAt),
            Returned returned => ReturnsColour(code, returned.At),
            Argument argument => TakesColour(code, argument),
            _ => false,
        };

        private bool AssignsColour(string code, int equals)
        {
            int lineStart = code.LastIndexOfAny([';', '{', '}', '\n'], Math.Max(0, equals - 1)) + 1;
            string before = code[lineStart..equals].TrimEnd();
            if (Regex.IsMatch(before, @"\bcolour\s+[A-Za-z_]\w*$") || Regex.IsMatch(before, @":\s*colour$"))
                return true;
            Match target = Regex.Match(before, @"(?:^|[^\w])([A-Za-z_]\w*)$");
            return target.Success && _names.Contains(target.Groups[1].Value);
        }

        /// <summary>Whether the innermost function around <paramref name="at"/> is declared to return a colour.</summary>
        private static bool ReturnsColour(string code, int at)
        {
            (int Body, string Type)? innermost = null;
            foreach (Match declaration in Declaration().Matches(code))
            {
                if (declaration.Index > at || !declaration.Groups[1].Success || NotTypes.Contains(declaration.Groups[1].Value))
                    continue;
                int close = Matching(code, declaration.Index + declaration.Length - 1);
                if (close < 0)
                    continue;
                int body = code.IndexOfAny(['{', ';'], close);
                if (body < 0 || code[body] != '{' || body > at)
                    continue;
                int end = Matching(code, body);
                if ((end < 0 || end > at) && (innermost is null || body > innermost.Value.Body))
                    innermost = (body, declaration.Groups[1].Value);
            }

            return innermost?.Type == "colour";
        }

        private bool TakesColour(string code, Argument argument)
        {
            Match name = Regex.Match(code[..argument.Open], @"([A-Za-z_]\w*)\s*$");
            if (!name.Success || NotTypes.Contains(name.Groups[1].Value))
                return false;
            int close = Matching(code, argument.Open);
            if (close < 0)
                return false;
            int count = Pieces(code, argument.Open + 1, close).Count;
            return _parameters.TryGetValue((name.Groups[1].Value, count), out bool[]? colours)
                   && colours is not null && argument.Index < colours.Length && colours[argument.Index];
        }

        private void Add(string name, List<string> parameters)
        {
            bool[] colours = [.. parameters.Select(IsColourParameter)];
            int required = parameters.Count(p => !p.Contains('='));
            // A call may leave off any of the defaulted parameters at the end.
            for (int count = required; count <= colours.Length; count++)
            {
                bool[] these = colours[..count];
                if (_parameters.TryGetValue((name, count), out bool[]? known) && (known is null || !known.SequenceEqual(these)))
                    _parameters[(name, count)] = null;
                else
                    _parameters[(name, count)] = these;
            }
        }

        private static bool IsColourParameter(string parameter)
        {
            string head = Regex.Replace(parameter.Split('=')[0], @"\[[^\]]*\]", " ").Trim();
            string? type = head.Split((char[])[' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(word => !Modifiers.Contains(word));
            return type == "colour";
        }
    }

    /// <summary>The index of the bracket closing (or opening, for a closer) the one at <paramref name="at"/>; -1 if none.</summary>
    private static int Matching(string code, int at)
    {
        char open = code[at];
        char close = open switch { '(' => ')', '[' => ']', '{' => '}', _ => '\0' };
        if (close == '\0')
            return -1;
        int depth = 0;
        for (int i = at; i < code.Length; i++)
        {
            if (code[i] == open)
                depth++;
            else if (code[i] == close && --depth == 0)
                return i;
        }

        return -1;
    }

    /// <summary>code[from..to) split at commas outside brackets, trimmed; empty pieces dropped.</summary>
    private static List<string> Pieces(string code, int from, int to)
    {
        var pieces = new List<string>();
        int depth = 0, start = from;
        for (int i = from; i <= to; i++)
        {
            if (i == to || (code[i] == ',' && depth == 0))
            {
                string piece = code[start..i].Trim();
                if (piece.Length > 0)
                    pieces.Add(piece);
                start = i + 1;
            }
            else if (code[i] is '(' or '[' or '{' or '<')
                depth++;
            else if (code[i] is ')' or ']' or '}' or '>')
                depth = Math.Max(0, depth - 1);
        }

        return pieces;
    }

    private static int[] LineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                starts.Add(i + 1);
        }

        return [.. starts];
    }

    /// <summary>The text with comments and strings turned to spaces (newlines kept), so places don't move.</summary>
    private static string Blank(string text)
    {
        var blanked = new StringBuilder(text);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n')
                    blanked[i++] = ' ';
            }
            else if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                int end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? text.Length : end + 2;
                for (; i < end; i++)
                {
                    if (text[i] != '\n')
                        blanked[i] = ' ';
                }

                i--;
            }
            else if (text[i] == '"')
            {
                int end = i + 1;
                while (end < text.Length && text[end] != '"' && text[end] != '\n')
                    end += text[end] == '\\' ? 2 : 1;
                for (; i <= Math.Min(end, text.Length - 1); i++)
                    blanked[i] = ' ';
                i--;
            }
        }

        return blanked.ToString();
    }
}
