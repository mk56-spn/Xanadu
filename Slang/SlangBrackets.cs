using System;
using System.Collections.Generic;

namespace Xanadu.Slang;

/// <summary>
/// One bracket in the code: where it is, what owns it, and where its partner is.
/// <see cref="Owner"/> is the kind of the thing it belongs to (<c>Triangle(</c>
/// is a library type's, <c>if (</c> a keyword's, a function body's braces its
/// function's), or null for plain grouping such as <c>(a + b)</c>.
/// </summary>
public readonly record struct Bracket(int Line, int Column, TokenKind? Owner, int Partner)
{
    public bool Matched => Partner >= 0;
}

/// <summary>
/// Pairs the brackets of the whole buffer and finds each pair's owner, so the
/// view can draw a bracket in its owner's colour instead of by nesting depth.
/// Brackets in comments and strings don't count.
/// </summary>
public static class SlangBrackets
{
    public static List<Bracket> Find(IReadOnlyList<string> lines, Func<string, List<Token>> tokenize)
    {
        var brackets = new List<Bracket>();
        var open = new Stack<(int Index, char Close)>();
        // The last character that isn't space, outside comments: lets a '{' after ')' take the ')''s owner.
        (int Line, int Column, int Bracket) last = (-1, -1, -1);

        for (int line = 0; line < lines.Count; line++)
        {
            string text = lines[line];
            List<Token> tokens = tokenize(text);
            for (int column = 0; column < text.Length; column++)
            {
                char c = text[column];
                Token? inside = At(tokens, column);
                if (inside is { Kind: TokenKind.Comment or TokenKind.Text })
                    continue;

                // [Default(…)]: the attribute token starts with its '['.
                bool attribute = inside is { Kind: TokenKind.Attribute } token && token.Start == column && c == '[';
                if (inside is not null && !attribute)
                {
                    last = (line, column, -1);
                    continue;
                }

                if (c is '(' or '[' or '{')
                {
                    TokenKind? owner = attribute ? TokenKind.Attribute
                        : c == '{' ? BraceOwner(brackets, last, tokens, text, column)
                        : Before(tokens, text, column);
                    open.Push((brackets.Count, c == '(' ? ')' : c == '[' ? ']' : '}'));
                    brackets.Add(new Bracket(line, column, owner, -1));
                    last = (line, column, brackets.Count - 1);
                }
                else if (c is ')' or ']' or '}')
                {
                    // Brackets left open inside this pair stay unmatched, so one missing ')'
                    // marks only itself, not every brace around it.
                    bool found = false;
                    foreach ((int _, char close) in open)
                    {
                        if (close == c)
                        {
                            found = true;
                            break;
                        }
                    }

                    while (found && open.Peek().Close != c)
                        open.Pop();
                    if (found)
                    {
                        int partner = open.Pop().Index;
                        brackets[partner] = brackets[partner] with { Partner = brackets.Count };
                        brackets.Add(new Bracket(line, column, brackets[partner].Owner, partner));
                    }
                    else
                        brackets.Add(new Bracket(line, column, null, -1));
                    last = (line, column, brackets.Count - 1);
                }
                else if (!char.IsWhiteSpace(c))
                    last = (line, column, -1);
            }
        }

        return brackets;
    }

    /// <summary>The kind of the word right before an opening '(' or '[', or null when it isn't one that owns brackets.</summary>
    private static TokenKind? Before(List<Token> tokens, string text, int column)
    {
        int at = column - 1;
        while (at >= 0 && text[at] == ' ')
            at--;
        if (at < 0)
            return null;

        Token? word = At(tokens, at);
        return word?.Kind switch
        {
            TokenKind.Width2 or TokenKind.Width3 or TokenKind.Width4 or TokenKind.Type => TokenKind.Type,
            TokenKind.LibraryType or TokenKind.Function or TokenKind.Keyword or TokenKind.Attribute => word.Value.Kind,
            _ => null,
        };
    }

    /// <summary>A brace takes the owner of the ')' just before it (a function's body, an if's block), or of a keyword (else, do).</summary>
    private static TokenKind? BraceOwner(List<Bracket> brackets, (int Line, int Column, int Bracket) last, List<Token> tokens, string text, int column)
    {
        if (last.Bracket >= 0 && brackets[last.Bracket] is { Matched: true } close && Closes(brackets, last.Bracket))
            return close.Owner;

        return Before(tokens, text, column) == TokenKind.Keyword ? TokenKind.Keyword : null;
    }

    private static bool Closes(List<Bracket> brackets, int index) => brackets[index].Partner < index;

    private static Token? At(List<Token> tokens, int column)
    {
        foreach (Token token in tokens)
        {
            if (column >= token.Start && column < token.End)
                return token;
        }

        return null;
    }
}
