namespace Xanadu.Ui.Editing;

/// <summary>
/// Leading spaces, and when they are off the indent steps: there is never a
/// reason for code to sit between steps, so such a line is marked and Enter
/// from it starts the new line on a step.
/// </summary>
public static class TextIndent
{
    /// <summary>The spaces a line starts with (all of them for a blank line).</summary>
    public static int Of(string line)
    {
        int spaces = 0;
        while (spaces < line.Length && line[spaces] == ' ')
            spaces++;
        return spaces;
    }

    /// <summary>A line with text that starts between two indent steps.</summary>
    public static bool Misaligned(string line)
    {
        int spaces = Of(line);
        return spaces < line.Length && spaces % TextBuffer.Indent != 0;
    }

    /// <summary>
    /// Where a line's text belongs: where it is when that's on a step, else the depth
    /// the owner says (<paramref name="suggested"/>), else the step below.
    /// </summary>
    public static int Wanted(string line, int? suggested)
    {
        int spaces = Of(line);
        if (spaces % TextBuffer.Indent == 0)
            return spaces;
        return suggested is >= 0 and var depth ? depth : spaces / TextBuffer.Indent * TextBuffer.Indent;
    }
}
