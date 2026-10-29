using System;
using Godot;
using Xanadu.Theme;

namespace Xanadu.Levels;

/// <summary>
/// A map's optional playfield background colour. It is stored in map files as
/// <c>#rrggbb</c>, or <c>#rrggbbaa</c> when it is translucent; an empty value
/// means the level uses the theme's stage colour.
/// </summary>
public static class LevelBackground
{
    /// <summary>The colour a playfield uses when its map does not set one.</summary>
    public static Color Default => Palette.Stage;

    /// <summary>Reads a stored colour, rejecting anything that is not a hex colour.</summary>
    public static bool TryParse(string? value, out Color color)
    {
        color = Default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string text = value.Trim();
        if (text.Length > 0 && text[0] != '#')
            text = "#" + text;

        if (text.Length is not (7 or 9))
            return false;

        for (int i = 1; i < text.Length; i++)
        {
            if (!char.IsAsciiHexDigit(text[i]))
                return false;
        }

        color = new Color(text);
        return true;
    }

    /// <summary>The stored spelling of a colour: alpha is only written when it is not opaque.</summary>
    public static string Format(Color color) =>
        "#" + color.ToHtml(color.A < 1f);

    /// <summary>The stored spelling of a colour, or empty when it cannot be read.</summary>
    public static string Normalize(string? value) =>
        TryParse(value, out Color color) ? Format(color) : "";
}
