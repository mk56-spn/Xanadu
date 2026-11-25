using System.Collections.Generic;
using System.Text;

namespace Xanadu.Slang;

/// <summary>Text helpers the BUILD brick's reader and a library's pass reader share.</summary>
internal static class BuildText
{
    /// <summary>The source with comments turned to spaces, every other character (and newline) where it was.</summary>
    public static string Blank(string source)
    {
        var text = new StringBuilder(source);
        for (int i = 0; i + 1 < text.Length; i++)
        {
            if (text[i] == '/' && text[i + 1] == '/')
            {
                for (; i < text.Length && text[i] != '\n'; i++)
                    text[i] = ' ';
            }
            else if (text[i] == '/' && text[i + 1] == '*')
            {
                text[i] = text[i + 1] = ' ';
                for (i += 2; i < text.Length && !(text[i] == '*' && i + 1 < text.Length && text[i + 1] == '/'); i++)
                    if (text[i] != '\n')
                        text[i] = ' ';
                if (i + 1 < text.Length)
                    text[i] = text[i + 1] = ' ';
                i++;
            }
        }

        return text.ToString();
    }

    /// <summary>The brace closing the one at <paramref name="open"/>, or -1.</summary>
    public static int Matching(string code, int open)
    {
        if (open < 0)
            return -1;
        int depth = 0;
        for (int i = open; i < code.Length; i++)
        {
            if (code[i] == '{')
                depth++;
            else if (code[i] == '}' && --depth == 0)
                return i;
        }

        return -1;
    }

    /// <summary>The text split at commas outside brackets.</summary>
    public static string[] SplitTop(string text)
    {
        var parts = new List<string>();
        int depth = 0, start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] is '(' or '[')
                depth++;
            else if (text[i] is ')' or ']')
                depth--;
            else if (text[i] == ',' && depth == 0)
            {
                parts.Add(text[start..i]);
                start = i + 1;
            }
        }

        parts.Add(text[start..]);
        return [.. parts];
    }

    /// <summary>
    /// The source with each span replaced by its text, the lines each took kept, so what follows
    /// stays on the line it was written on (errors land where they were written).
    /// </summary>
    public static string Replaced(string source, IEnumerable<(int Start, int End, string Text)> spans)
    {
        var text = new StringBuilder();
        int at = 0;
        foreach ((int start, int end, string replaced) in System.Linq.Enumerable.OrderBy(spans, span => span.Start))
        {
            text.Append(source, at, start - at);
            text.Append(replaced);
            text.Append('\n', source.AsSpan(start, end - start).Count('\n'));
            at = end;
        }

        text.Append(source, at, source.Length - at);
        return text.ToString();
    }
}
