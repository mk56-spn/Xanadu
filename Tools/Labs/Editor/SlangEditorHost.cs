using System;
using System.Collections.Generic;
using System.Linq;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;
using Xanadu.Theme;
using Xanadu.Ui.Editing;

namespace Xanadu.Labs.Editor;

/// <summary>
/// What turns the UI's plain <see cref="TextEditor"/> into the Slang editor:
/// each token in its kind's colour, brackets in their owner's colour (the pair
/// at the caret lit and boxed, an unmatched one red), the library's parameter
/// names as inlays, problems as marked lines, and the completion list taking
/// its keys and clicks first. Enter after <c>{</c> indents a step; <c>}</c>
/// typed on an empty line steps back one. An opening bracket brings its
/// closer with the caret between; typing that closer steps over it, and
/// Backspace inside an empty pair removes both.
/// <para>
/// F2 on a name the code declares renames it in place: every place of it is
/// boxed and typing changes them all. Enter, or the caret leaving the name, keeps
/// the new name; Esc puts the old one back.
/// </para>
/// </summary>
internal sealed class SlangEditorHost(SlangHints hints, CompletionPopup popup) : ITextEditorHost
{
    /// <summary>A fixed colour per kind of token (not editable yet).</summary>
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
        [TokenKind.Namespace] = new("#89ddcb"),
        [TokenKind.Function] = new("#82aaff"),
        [TokenKind.Number] = new("#f78c6c"),
        [TokenKind.Text] = new("#c3e88d"),
        [TokenKind.Attribute] = new("#a0a8c4"),
        [TokenKind.Comment] = new("#6b7a99"),
    };

    /// <summary>How much of its owner's colour a bracket takes, the rest being the background: dimmer than the name, so the name leads.</summary>
    private const float BracketStrength = 0.7f;

    private static readonly Color Background = new("#0b1020");
    private static readonly Color ErrorLine = new(0.94f, 0.27f, 0.27f, 0.12f);
    private static readonly Color WarningLine = new(0.98f, 0.75f, 0.14f, 0.10f);

    /// <summary>Each opening bracket's closer, typed for it.</summary>
    private static readonly Dictionary<char, char> Pairs = new() { ['('] = ')', ['['] = ']', ['{'] = '}' };

    private static readonly HashSet<char> Closers = [')', ']', '}'];

    /// <summary>A rename under way: the name as it was, as it is now, and its places (the caret's one first).</summary>
    private sealed class Renaming(string original, List<(int Line, int Column)> places)
    {
        public readonly string Original = original;
        public string Name = original;
        public readonly List<(int Line, int Column)> Places = places;
    }

    private Renaming? _rename;

    /// <summary>Whether a rename is under way: completion stays shut meanwhile.</summary>
    public bool IsRenaming => _rename is not null;

    /// <summary>Ends a rename where it stands (undo moved the text under it).</summary>
    public void StopRename() => _rename = null;

    /// <summary>This frame's bracket colours by line, column.</summary>
    private readonly Dictionary<(int Line, int Column), Color> _brackets = new();

    /// <summary>What Slang and the lint found, by 1-based line; the first on a line is the one shown.</summary>
    public IReadOnlyList<SlangProblem> Problems { get; set; } = [];

    private List<Token> Tokens(string line) => SlangSyntax.Tokens(line, hints.Types, hints.Namespaces, hints.Aliases);

    public void Prepare(TextBuffer buffer, List<TextCell> boxed)
    {
        _brackets.Clear();
        FollowRename(buffer, boxed);
        List<Bracket> brackets = SlangBrackets.Find(buffer.Lines, Tokens);

        // The bracket just after the caret, else the one just before it.
        int active = brackets.FindIndex(b => b.Line == buffer.Line && b.Column == buffer.Column);
        if (active < 0)
            active = brackets.FindIndex(b => b.Line == buffer.Line && b.Column == buffer.Column - 1);
        int partner = active >= 0 ? brackets[active].Partner : -1;

        for (int i = 0; i < brackets.Count; i++)
        {
            Bracket bracket = brackets[i];
            Color full = !bracket.Matched ? Palette.DangerBright : bracket.Owner is { } owner ? Colours[owner] : TextEditorDrawSystem.Plain;
            bool lit = bracket.Matched && (i == active || i == partner);
            _brackets[(bracket.Line, bracket.Column)] = lit || !bracket.Matched ? full : Background.Lerp(full, BracketStrength);
        }

        if (partner >= 0)
        {
            boxed.Add(new TextCell(brackets[active].Line, brackets[active].Column));
            boxed.Add(new TextCell(brackets[partner].Line, brackets[partner].Column));
        }
    }

    public void Decorate(TextBuffer buffer, int line, TextLineDecor decor)
    {
        string text = buffer.Lines[line];
        // Each column's colour: its token's, then a bracket's over it; runs are where it stays the same.
        var colours = new Color?[text.Length];
        foreach (Token token in Tokens(text))
        {
            for (int c = token.Start; c < token.End && c < text.Length; c++)
                colours[c] = Colours[token.Kind];
        }

        for (int c = 0; c < text.Length; c++)
        {
            if (_brackets.TryGetValue((line, c), out Color bracket))
                colours[c] = bracket;
        }

        for (int start = 0; start < text.Length;)
        {
            int end = start + 1;
            while (end < text.Length && colours[end] == colours[start])
                end++;
            if (colours[start] is { } colour)
                decor.Runs.Add(new TextRun(start, end, colour));
            start = end;
        }

        foreach (InlayHint hint in hints.For(text))
            decor.Inlays.Add(new TextInlay(hint.Column, hint.Label));

        foreach (SlangProblem problem in Problems)
        {
            if (problem.Line != line + 1)
                continue;
            decor.Mark = new TextLineMark(
                problem.Warning ? WarningLine : ErrorLine, problem.Warning ? Palette.Warning : Palette.DangerBright,
                Math.Max(0, problem.Column - 1), problem.Message);
            break;
        }
    }

    public bool TakeKey(TextBuffer buffer, Key key, bool shift, bool ctrl)
    {
        if (key == Key.F2)
        {
            StartRename(buffer);
            return _rename is not null;
        }

        if (_rename is not null && key is Key.Escape or Key.Enter or Key.KpEnter)
        {
            if (key == Key.Escape)
                Rename(buffer, _rename.Original);
            _rename = null;
            return true;
        }

        if (popup.Open)
        {
            if (key == Key.Escape)
            {
                popup.Dismiss(buffer);
                return true;
            }

            // While the list is open its keys choose and take, not move the caret.
            if (!shift && key is Key.Up or Key.Down or Key.Enter or Key.Tab)
            {
                if (key == Key.Up)
                    popup.Move(-1);
                else if (key == Key.Down)
                    popup.Move(1);
                else
                    popup.Accept(buffer);
                return true;
            }
        }

        string line = buffer.Lines[buffer.Line];
        char? previous = buffer.Column > 0 ? line[buffer.Column - 1] : null;
        char? next = buffer.Column < line.Length ? line[buffer.Column] : null;

        // Backspace inside an empty pair takes both brackets.
        if (key == Key.Backspace && !buffer.HasSelection && previous is { } open && Pairs.TryGetValue(open, out char close) && next == close)
        {
            buffer.Replace(buffer.Column - 1, buffer.Column + 1, "");
            return true;
        }

        // Enter after an opening brace indents the new line one step more; between {} the } goes to a line of its own.
        if (key is Key.Enter or Key.KpEnter && line[..buffer.Column].TrimEnd().EndsWith('{'))
        {
            bool closing = !buffer.HasSelection && line[buffer.Column..].TrimStart().StartsWith('}');
            buffer.NewLine(TextBuffer.Indent);
            if (closing)
            {
                int caret = buffer.Column;
                buffer.NewLine(-TextBuffer.Indent);
                buffer.Place(buffer.Line - 1, caret);
            }

            return true;
        }

        return false;
    }

    public bool TakeTyped(TextBuffer buffer, string text)
    {
        string line = buffer.Lines[buffer.Line];
        char? next = buffer.Column < line.Length ? line[buffer.Column] : null;
        if (text.Length == 1 && !buffer.HasSelection)
        {
            char typed = text[0];
            // A closer typed in front of the same closer steps over it.
            if (Closers.Contains(typed) && next == typed)
            {
                buffer.MoveColumn(1);
                return true;
            }

            // An opener brings its closer, the caret between, unless it sits right before other text.
            if (Pairs.TryGetValue(typed, out char close) && (next is null || char.IsWhiteSpace(next.Value) || Closers.Contains(next.Value) || next is ';' or ','))
            {
                buffer.Insert($"{typed}{close}");
                buffer.MoveColumn(-1);
                return true;
            }
        }

        // A closing brace typed on a line of only spaces steps back one indent.
        string before = line[..buffer.Column];
        if (text != "}" || buffer.HasSelection || before.Length < TextBuffer.Indent || before.Trim().Length != 0)
            return false;
        buffer.Replace(buffer.Column - TextBuffer.Indent, buffer.Column, "}");
        return true;
    }

    public bool TakePress(TextBuffer buffer, Vector2 point) => popup.Click(buffer, point);

    /// <summary>Starts renaming the name at (or just before) the caret, if the code declares it.</summary>
    private void StartRename(TextBuffer buffer)
    {
        _rename = null;
        var found = SlangRename.Find(buffer.Lines, buffer.Line, buffer.Column);
        if (found is null && buffer.Column > 0)
            found = SlangRename.Find(buffer.Lines, buffer.Line, buffer.Column - 1);
        if (found is not { } rename)
            return;
        // The place the caret is in goes first: it is the one typed into.
        int mine = rename.Places.FindIndex(p => p.Line == buffer.Line && p.Column <= buffer.Column && buffer.Column <= p.Column + rename.Name.Length);
        if (mine < 0)
            return;
        var places = new List<(int Line, int Column)>(rename.Places);
        (places[0], places[mine]) = (places[mine], places[0]);
        _rename = new Renaming(rename.Name, places);
        popup.Dismiss(buffer);
    }

    /// <summary>
    /// Once a frame: what was typed into the caret's place goes to all the others, and they are
    /// boxed; the rename ends when the caret leaves the name.
    /// </summary>
    private void FollowRename(TextBuffer buffer, List<TextCell> boxed)
    {
        if (_rename is not { } rename)
            return;
        (int line, int column) = rename.Places[0];
        string text = line < buffer.Lines.Count ? buffer.Lines[line] : "";
        int end = column;
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
            end++;
        if (buffer.Line != line || buffer.Column < column || buffer.Column > end || column > text.Length)
        {
            _rename = null;
            return;
        }

        string typed = text[column..end];
        if (typed != rename.Name)
        {
            // Every other place must still hold the name; when one doesn't (an undo), the rename is over.
            for (int i = 1; i < rename.Places.Count; i++)
            {
                (int l, int c) = rename.Places[i];
                string other = l < buffer.Lines.Count ? buffer.Lines[l] : "";
                int shift = l == line && c > column ? typed.Length - rename.Name.Length : 0;
                if (c + shift + rename.Name.Length > other.Length || string.CompareOrdinal(other, c + shift, rename.Name, 0, rename.Name.Length) != 0)
                {
                    _rename = null;
                    return;
                }
            }

            // The caret's place already holds it; places after it on its line moved with the typing.
            int moved = typed.Length - rename.Name.Length;
            for (int i = 1; i < rename.Places.Count; i++)
            {
                if (rename.Places[i].Line == line && rename.Places[i].Column > column)
                    rename.Places[i] = (line, rename.Places[i].Column + moved);
            }

            string before = rename.Name;
            rename.Name = typed;
            Rewrite(buffer, rename, before, skipFirst: true);
        }

        foreach ((int l, int c) in rename.Places)
        {
            for (int i = 0; i < rename.Name.Length; i++)
                boxed.Add(new TextCell(l, c + i));
        }
    }

    /// <summary>Gives every place the name, the caret's one included.</summary>
    private void Rename(TextBuffer buffer, string name)
    {
        if (_rename is not { } rename || rename.Name == name)
            return;
        string before = rename.Name;
        rename.Name = name;
        Rewrite(buffer, rename, before, skipFirst: false);
    }

    /// <summary>Writes the rename's name over each place that holds <paramref name="before"/>, left to right, keeping later places on a line in step.</summary>
    private static void Rewrite(TextBuffer buffer, Renaming rename, string before, bool skipFirst)
    {
        int moved = rename.Name.Length - before.Length;
        var order = Enumerable.Range(0, rename.Places.Count).OrderBy(i => rename.Places[i].Line).ThenBy(i => rename.Places[i].Column).ToList();
        foreach (int i in order)
        {
            if (skipFirst && i == 0)
                continue;
            (int line, int column) = rename.Places[i];
            buffer.Edit(line, column, column + before.Length, rename.Name);
            // Everything after it on its line, the caret's place too, shifts by the change.
            for (int j = 0; j < rename.Places.Count; j++)
            {
                if (j != i && rename.Places[j].Line == line && rename.Places[j].Column > column)
                    rename.Places[j] = (line, rename.Places[j].Column + moved);
            }
        }
    }
}
