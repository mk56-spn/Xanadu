using System.Collections.Generic;

namespace Xanadu.Ecs;

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

    private static bool Fits(string text, int fontSize, float width) =>
        UiNode.Measure(text, fontSize).X <= width;
}
