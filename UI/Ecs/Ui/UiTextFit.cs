using System.Collections.Generic;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>Fitting text into a width, measured rather than guessed from a character count.</summary>
public static class UiTextFit
{
    private const string Ellipsis = "...";

    /// <summary>The text, cut short with an ellipsis if it is wider than a box of this width.</summary>
    public static string Cut(string text, int fontSize, float width)
    {
        if (Fits(text, fontSize, width))
            return text;

        int length = text.Length;
        while (length > 0 && !Fits(text[..length] + Ellipsis, fontSize, width))
            length--;
        return text[..length] + Ellipsis;
    }

    /// <summary>Breaks the text into lines that each fit a box of this width.</summary>
    public static List<string> Wrap(string text, int fontSize, float width)
    {
        var lines = new List<string>();
        int start = 0;
        while (start < text.Length)
        {
            int end = start + 1;
            while (end < text.Length && Fits(text[start..(end + 1)], fontSize, width))
                end++;
            lines.Add(text[start..end]);
            start = end;
        }
        return lines;
    }

    /// <summary>
    /// Breaks the text into lines at spaces so each fits a box of this width; a
    /// newline starts a new line, and a word too long for a line of its own is
    /// cut where it runs out of room.
    /// </summary>
    public static List<string> WrapWords(string text, int fontSize, float width)
    {
        var lines = new List<string>();
        foreach (string paragraph in text.Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' '))
            {
                string joined = line.Length == 0 ? word : line + " " + word;
                if (Fits(joined, fontSize, width))
                {
                    line = joined;
                    continue;
                }

                if (line.Length > 0)
                    lines.Add(line);
                line = word;
                if (Fits(word, fontSize, width))
                    continue;

                List<string> pieces = Wrap(word, fontSize, width);
                lines.AddRange(pieces.GetRange(0, pieces.Count - 1));
                line = pieces[^1];
            }

            lines.Add(line);
        }

        return lines;
    }

    /// <summary>One line's height at this size: wrapped text is this times its line count.</summary>
    public static float LineHeight(int fontSize) => fontSize * UiNode.TextLineHeight;

    /// <summary>How tall the text is once wrapped to this width.</summary>
    public static float WrappedHeight(string text, int fontSize, float width) =>
        WrapWords(text, fontSize, width).Count * LineHeight(fontSize);

    private static bool Fits(string text, int fontSize, float width) =>
        UiNode.Measure(text, fontSize).X <= width;
}
