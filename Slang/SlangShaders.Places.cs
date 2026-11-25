using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>Where a place in a composed body is: the <c>#line</c> marks each of a stack's files carries.</summary>
public static partial class SlangShaders
{
    /// <summary>
    /// Every <c>#line 1 "label"</c> mark in a body: the label and the 1-based line of the
    /// body the label's line 1 is. A stack's files each carry one (<see cref="ShaderStack.Compose"/>).
    /// </summary>
    public static List<(string Label, int Line)> LineMarks(string source)
    {
        var marks = new List<(string, int)>();
        string[] lines = source.ReplaceLineEndings("\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            Match mark = Regex.Match(lines[i], "^\\s*#line\\s+1\\s+\"([^\"]*)\"");
            if (mark.Success)
                marks.Add((mark.Groups[1].Value, i + 2));
        }

        return marks;
    }

    /// <summary>A place in a body (a character index) as Slang would name it: <c>label(line)</c>, under the last mark before it.</summary>
    public static string Where(string name, string source, int index)
    {
        int line = 1 + source.AsSpan(0, Math.Min(index, source.Length)).Count('\n');
        string label = name;
        int first = 1;
        foreach ((string marked, int at) in LineMarks(source))
        {
            if (at > line)
                break;
            (label, first) = (marked, at);
        }

        return $"{label}({line - first + 1})";
    }
}
