using System;
using System.Collections.Generic;

namespace Xanadu.Labs.Editor;

/// <summary>What a piece of a line is, for its colour.</summary>
internal enum TokenKind
{
    Plain,
    Keyword,
    Type,

    /// <summary>The width digits of a vector or matrix type (<c>float</c><b>3</b>, <c>float</c><b>4x4</b>), brighter as it grows.</summary>
    Width2,
    Width3,
    Width4,
    LibraryType,
    Namespace,
    Function,
    Number,
    Text,
    Attribute,
    Comment,
}

/// <summary>A run of one kind in a line: columns [Start, End).</summary>
internal readonly record struct Token(int Start, int End, TokenKind Kind);

/// <summary>
/// Splits one line of Slang into coloured runs. Line by line and forgiving: a
/// block comment is only recognised when it closes on its own line, and
/// anything it doesn't know is plain. Library types and namespaces come from
/// the library itself (<see cref="SlangHints"/>).
/// </summary>
internal static class SlangSyntax
{
    private static readonly HashSet<string> Keywords =
    [
        "if", "else", "for", "while", "do", "return", "break", "continue", "discard", "switch", "case", "default",
        "let", "var", "const", "static", "uniform", "in", "out", "inout", "import", "module", "struct", "interface",
        "extension", "namespace", "public", "internal", "private", "true", "false", "this", "This", "enum", "typedef",
        "__init", "__exported",
    ];

    private static readonly HashSet<string> Types =
    [
        "void", "bool", "int", "uint", "half", "float", "double",
        "bool2", "bool3", "bool4", "int2", "int3", "int4", "uint2", "uint3", "uint4",
        "half2", "half3", "half4", "float2", "float3", "float4",
        "float2x2", "float3x3", "float4x4", "Sampler2D", "Texture2D", "SamplerState", "ConstantBuffer",
        "ItemInput", "ItemVaryings",
    ];

    public static List<Token> Tokens(string line, IReadOnlySet<string> libraryTypes, IReadOnlySet<string> namespaces, IReadOnlySet<string>? aliases = null)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < line.Length)
        {
            char c = line[i];
            int start = i;

            if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                tokens.Add(new Token(i, line.Length, TokenKind.Comment));
                break;
            }

            if (c == '/' && i + 1 < line.Length && line[i + 1] == '*')
            {
                int close = line.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = close < 0 ? line.Length : close + 2;
                tokens.Add(new Token(start, i, TokenKind.Comment));
                continue;
            }

            if (c == '"')
            {
                i++;
                while (i < line.Length && line[i] != '"')
                    i += line[i] == '\\' ? 2 : 1;
                i = Math.Min(line.Length, i + 1);
                tokens.Add(new Token(start, i, TokenKind.Text));
                continue;
            }

            // [Default(1)] and friends: the attribute's name in brackets.
            if (c == '[' && i + 1 < line.Length && char.IsLetter(line[i + 1]))
            {
                int name = i + 1;
                while (name < line.Length && (char.IsLetterOrDigit(line[name]) || line[name] == '_'))
                    name++;
                tokens.Add(new Token(start, name, TokenKind.Attribute));
                i = name;
                continue;
            }

            if (char.IsDigit(c) || (c == '.' && i + 1 < line.Length && char.IsDigit(line[i + 1])))
            {
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '.'))
                    i++;
                tokens.Add(new Token(start, i, TokenKind.Number));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                    i++;
                string word = line[start..i];
                TokenKind kind = Classify(word, line, i, libraryTypes, namespaces, aliases);
                int digits = kind == TokenKind.Type ? WidthStart(word) : -1;
                if (digits > 0)
                {
                    // float4: "float" as a type, "4" stepped by width, so 2, 3 and 4 tell apart at a glance.
                    tokens.Add(new Token(start, start + digits, TokenKind.Type));
                    tokens.Add(new Token(start + digits, i, Width(word[digits])));
                }
                else
                    tokens.Add(new Token(start, i, kind));
                continue;
            }

            i++;
        }

        return tokens;
    }

    /// <summary>Where a vector or matrix type's width starts (5 in <c>float4</c>), or -1 for a type without one.</summary>
    private static int WidthStart(string type)
    {
        int digits = type.Length;
        while (digits > 0 && (char.IsDigit(type[digits - 1]) || type[digits - 1] == 'x'))
            digits--;
        return digits > 0 && digits < type.Length && char.IsDigit(type[digits]) ? digits : -1;
    }

    private static TokenKind Width(char first) => first switch
    {
        '2' => TokenKind.Width2,
        '3' => TokenKind.Width3,
        _ => TokenKind.Width4,
    };

    private static TokenKind Classify(string word, string line, int end, IReadOnlySet<string> libraryTypes, IReadOnlySet<string> namespaces, IReadOnlySet<string>? aliases)
    {
        if (Keywords.Contains(word))
            return TokenKind.Keyword;
        // An alias (colour) is another name for a built-in type, so it looks like one.
        if (Types.Contains(word) || aliases?.Contains(word) == true)
            return TokenKind.Type;
        if (libraryTypes.Contains(word))
            return TokenKind.LibraryType;

        int next = end;
        while (next < line.Length && line[next] == ' ')
            next++;
        if (next < line.Length && line[next] == '.' && namespaces.Contains(word))
            return TokenKind.Namespace;
        if (next < line.Length && line[next] == '(')
            return TokenKind.Function;
        return TokenKind.Plain;
    }
}
