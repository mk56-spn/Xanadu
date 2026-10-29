namespace Xanadu.Ecs;

/// <summary>
/// A rough width for a line of text, for sizing boxes before a font is at
/// hand. It runs a little generous, since text wider than its box is clipped.
/// The advances were calibrated against the shipped font: lowercase needs
/// ~0.62em, caps ~0.70em. Keep the bias high — a box a glyph short silently
/// drops that glyph.
/// </summary>
public static class UiTextMetrics
{
    /// <summary>Estimated width in pixels of <paramref name="text"/> at <paramref name="fontSize"/>.</summary>
    public static float Width(string text, float fontSize, bool bold = false)
    {
        float em = 0;
        foreach (char c in text)
            em += Advance(c);

        return em * fontSize * (bold ? 1.06f : 1f) * 0.93f;
    }

    /// <summary>The advance of one character, in ems.</summary>
    private static float Advance(char c) => c switch
    {
        ' ' => 0.32f,
        'i' or 'l' or 'j' or '!' or '|' or '.' or ',' or ':' or ';' or '\'' => 0.28f,
        'I' or 't' or 'f' or 'r' or '(' or ')' or '[' or ']' or '1' => 0.36f,
        'm' or 'M' => 0.86f,
        'w' or 'W' => 0.84f,
        >= 'A' and <= 'Z' => 0.74f,
        >= '0' and <= '9' => 0.60f,
        >= 'a' and <= 'z' => 0.62f,
        _ => 0.6f,
    };
}
