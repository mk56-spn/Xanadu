using System.Collections.Generic;
using Xanadu.Core.Theme;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>The Slang editor's fixed colour per kind of token (not editable yet).</summary>
internal static class SlangTokenColours
{
    private static readonly Dictionary<TokenKind, Color> Colours = new()
    {
        [TokenKind.Plain] = TextEditorDrawSystem.Plain,
        [TokenKind.Keyword] = new("#c792ea"),
        [TokenKind.Type] = new("#7fb8e6"),
        // Same blue, brighter as the width grows.
        [TokenKind.Width2] = new("#a3cdf2"),
        [TokenKind.Width3] = new("#c4e0fa"),
        [TokenKind.Width4] = new("#eaf5ff"),
        [TokenKind.LibraryType] = new("#ffcb6b"),
        // The code's own types: the library's yellow, paler.
        [TokenKind.CodeType] = new("#ffe4a3"),
        [TokenKind.Namespace] = new("#89ddcb"),
        [TokenKind.Function] = new("#82aaff"),
        [TokenKind.Number] = new("#f78c6c"),
        [TokenKind.Text] = new("#c3e88d"),
        [TokenKind.Attribute] = new("#4fa35e"),
        // Ours stand apart from Slang's: [Default], [Instance] and any the library adds.
        [TokenKind.LibraryAttribute] = new("#ff9cac"),
        [TokenKind.Comment] = new("#6b7a99"),
        // The library's constants (PI, particles.LINE): a number's family, warmer.
        [TokenKind.LibraryConstant] = new("#ffb86c"),
        // Constants, uniforms and the stack's state, wherever they are used: a quiet warm tint, there are many of them.
        [TokenKind.Global] = new("#dcc9a8"),
        // A field after a dot: tinted off the plain text, quieter than a call.
        [TokenKind.Member] = new("#a6c4dc"),
    };

    public static Color Of(TokenKind kind) => Colours[kind];
}
