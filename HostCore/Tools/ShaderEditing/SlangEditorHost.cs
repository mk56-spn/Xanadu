using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xanadu.Core.Theme;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// What turns the UI's plain <see cref="TextEditor"/> into the Slang editor:
/// each token in its kind's colour, brackets in their owner's colour (the pair
/// at the caret lit and boxed, an unmatched one red), each loop's index in a
/// colour by its nesting, colour swizzles by channel, names that do nothing
/// faded (<see cref="SlangUnused"/>), the library's parameter names as inlays,
/// a colour square in front of each colour constant (<see cref="SlangColours"/>),
/// problems as marked lines, and the completion list taking
/// its keys and clicks first. Enter after <c>{</c> indents a step; <c>}</c>
/// typed on an empty line steps back one. An opening bracket brings its
/// closer with the caret between; typing that closer steps over it, and
/// Backspace inside an empty pair removes both.
/// <para>
/// F2 on a name the code declares renames it in place: every place of it is
/// boxed and typing changes them all. Enter, or the caret leaving the name, keeps
/// the new name; Esc puts the old one back.
/// </para>
/// <para>
/// F12, or Ctrl+click, on a name the code declares puts the caret on its declaration;
/// a name declared elsewhere (the library's) goes to <see cref="GoElsewhere"/>.
/// </para>
/// </summary>
internal sealed partial class SlangEditorHost(SlangHints hints, SlangColours colourConstants, CompletionPopup popup) : ITextEditorHost
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
        [TokenKind.Attribute] = new("#4fa35e"),
        // Ours stand apart from Slang's: [Default], [Instance] and any the library adds.
        [TokenKind.LibraryAttribute] = new("#ff9cac"),
        [TokenKind.Comment] = new("#6b7a99"),
    };

    /// <summary>Each letter of a colour swizzle (<c>.rgb</c>, <c>.a</c>, <c>.bgra</c>) in its own channel's colour.</summary>
    private static readonly Dictionary<char, Color> Channels = new()
    {
        ['r'] = new("#ff7a7a"),
        ['g'] = new("#7fe0a0"),
        ['b'] = new("#7ab8ff"),
        ['a'] = new("#c9cfe0"),
    };

    /// <summary>A loop's index, by how deep the loop is nested (cycling past the last), so nested ones read apart.</summary>
    private static readonly Color[] LoopIndexes = [new("#ff8fb8"), new("#9fe870"), new("#7fd6e8")];

    /// <summary>How much of its colour a name that does nothing keeps, the rest being the background.</summary>
    private const float FadedStrength = 0.4f;

    /// <summary>How much of its owner's colour a bracket takes, the rest being the background: dimmer than the name, so the name leads.</summary>
    private const float BracketStrength = 0.7f;

    private static readonly Color Background = Palette.Background;
    private static readonly Color ErrorLine = new(0.94f, 0.27f, 0.27f, 0.12f);
    private static readonly Color WarningLine = new(0.98f, 0.75f, 0.14f, 0.10f);

    /// <summary>Each opening bracket's closer, typed for it.</summary>
    private static readonly Dictionary<char, char> Pairs = new() { ['('] = ')', ['['] = ']', ['{'] = '}' };

    private static readonly HashSet<char> Closers = [')', ']', '}'];

    /// <summary>Ctrl+S in the code; null leaves the key to nobody.</summary>
    public Action? Save { get; init; }

    /// <summary>The colour each stack entry's name takes where it is declared (<c>shade</c>, <c>deform</c>, …).</summary>
    public IReadOnlyDictionary<string, Color> EntryColours { get; init; } = new Dictionary<string, Color>();

    [GeneratedRegex(@"^\s*[\w:<>]+\s+(\w+)\s*\(")]
    private static partial Regex Declared();

    /// <summary>Go to the declaration of the name at the caret when this text doesn't declare it; true when there is one.</summary>
    public Func<TextBuffer, bool>? GoElsewhere { get; init; }

    /// <summary>The text can't be edited: F2 doesn't start a rename.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Drops what was worked out for the text, after another text was put in the editor.</summary>
    public void Forget()
    {
        _rename = null;
        _fadedVersion = -1;
    }

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
    private readonly Dictionary<(int Line, int Column), Color> _brackets = [];

    /// <summary>What Slang and the lint found, by 1-based line; the first on a line is the one shown.</summary>
    public IReadOnlyList<SlangProblem> Problems
    {
        get;
        set
        {
            if (!ReferenceEquals(field, value))
                _version++;
            field = value;
        }
    } = [];

    /// <summary>
    /// The float3s the last compile took as colours, with the text each covered: a hint shows only
    /// while that text is still where it was, so edits since don't leave it hanging on something else.
    /// </summary>
    public IReadOnlyList<(ColourConversion Place, string Text)> ColourConversions
    {
        get;
        set
        {
            if (!ReferenceEquals(field, value))
                _version++;
            field = value;
        }
    } = [];

    private int _version;

    /// <summary>Problems or conversions swapped for a new list, a rename begun, typed or ended, the library's names read.</summary>
    public int Version => HashCode.Combine(_version, hints.Version, _rename, _rename?.Name, ReadOnly);

    /// <summary>The inlay in front of a float3 taken as a colour, read as the cast it stands for.</summary>
    private const string ConversionLabel = "(colour)";

    /// <summary>The names that do nothing, found again whenever the text changes.</summary>
    public IReadOnlyList<Faded> Faded => _faded;

    private List<Faded> _faded = [];
    private List<LoopIndex> _loopIndexes = [];
    private Dictionary<string, System.Numerics.Vector4> _declaredColours = [];
    private int _fadedVersion = -1;

    /// <summary>Why the name at a place is faded, or null when it isn't.</summary>
    public string? FadedAt(int line, int column)
    {
        foreach (Faded faded in _faded)
        {
            if (faded.Line == line && faded.Column <= column && column < faded.Column + faded.Length)
                return faded.Reason;
        }

        return null;
    }

    private List<Token> Tokens(string line) => SlangSyntax.Tokens(line, hints.Types, hints.Namespaces, hints.Aliases, hints.Attributes);

    public void Prepare(TextBuffer buffer, List<TextCell> boxed)
    {
        _brackets.Clear();
        FollowRename(buffer, boxed);
        if (_fadedVersion != buffer.Version)
        {
            _fadedVersion = buffer.Version;
            _faded = SlangUnused.Find(buffer.Lines);
            _loopIndexes = SlangLoops.Find(buffer.Lines);
            _declaredColours = SlangColours.Declared(buffer.Lines);
        }

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

        // A stack entry where it is declared (a type, the name, then its bracket), in its role's colour.
        if (EntryColours.Count > 0 && Declared().Match(text) is { Success: true } declared
            && EntryColours.TryGetValue(declared.Groups[1].Value, out Color entry))
        {
            Group name = declared.Groups[1];
            for (int c = name.Index; c < name.Index + name.Length; c++)
                colours[c] = entry;
        }

        foreach (LoopIndex index in _loopIndexes)
        {
            if (index.Line != line)
                continue;
            for (int c = index.Column; c < index.Column + index.Length && c < text.Length; c++)
                colours[c] = LoopIndexes[index.Depth % LoopIndexes.Length];
        }

        // A swizzle of colour channels only: right after a dot, one to four of r g b a, then the word ends.
        for (int dot = text.IndexOf('.'); dot >= 0; dot = text.IndexOf('.', dot + 1))
        {
            int end = dot + 1;
            while (end < text.Length && Channels.ContainsKey(text[end]))
                end++;
            bool swizzle = end - dot - 1 is >= 1 and <= 4 && (end == text.Length || !(char.IsLetterOrDigit(text[end]) || text[end] == '_'))
                           && dot > 0 && !char.IsDigit(text[dot - 1]);
            for (int c = dot + 1; swizzle && c < end; c++)
                colours[c] = Channels[text[c]];
        }

        foreach (Faded faded in _faded)
        {
            if (faded.Line != line)
                continue;
            for (int c = faded.Column; c < faded.Column + faded.Length && c < text.Length; c++)
                colours[c] = Background.Lerp(colours[c] ?? TextEditorDrawSystem.Plain, FadedStrength);
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
        foreach ((ColourConversion place, string covered) in ColourConversions)
        {
            if (place.Line == line && place.Column + covered.Length <= text.Length
                && string.CompareOrdinal(text, place.Column, covered, 0, covered.Length) == 0)
                decor.Inlays.Add(new TextInlay(place.Column, ConversionLabel));
        }
        foreach (ColourUse use in colourConstants.In(text, _declaredColours))
            decor.Inlays.Add(TextInlay.ColourSwatch(use.Column, new Color(use.Colour.X, use.Colour.Y, use.Colour.Z, use.Colour.W)));
        // Swatches go in front of a name, hints in front of an argument: at one column the swatch sits nearer the text.
        decor.Inlays.Sort((a, b) => a.Column != b.Column ? a.Column.CompareTo(b.Column) : (a.Swatch is null ? 0 : 1).CompareTo(b.Swatch is null ? 0 : 1));

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
        if (key == Key.S && ctrl)
        {
            Save?.Invoke();
            return Save is not null;
        }

        if (key == Key.F12)
            return GoToDeclaration(buffer);

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
        if (key == Key.Backspace && !ctrl && !buffer.HasSelection && previous is { } open && Pairs.TryGetValue(open, out char close) && next == close)
        {
            buffer.Replace(buffer.Column - 1, buffer.Column + 1, "");
            return true;
        }

        // Enter after an opening brace indents the new line one step more; between {} the } goes to a line of its own.
        if (key is Key.Enter or Key.KpEnter && line[..buffer.Column].TrimEnd().EndsWith('{'))
        {
            bool closing = !buffer.HasSelection && line[buffer.Column..].TrimStart().StartsWith('}');
            buffer.DeleteSelection();
            buffer.NewLine(TextBuffer.Indent, ((ITextEditorHost)this).WantedIndent(buffer, buffer.Line));
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

    /// <summary>Each line's depth by the braces open at its start, -1 inside an open ( or [ (a continuation, deeper by choice).</summary>
    private int[] _depths = [];
    private int _depthsVersion = -1;

    /// <summary>A step per brace open at the line's start, one fewer when it starts with the closing one.</summary>
    public int? IndentFor(TextBuffer buffer, int line)
    {
        if (_depthsVersion != buffer.Version)
        {
            _depthsVersion = buffer.Version;
            _depths = Depths(buffer.Lines);
        }

        if (line >= _depths.Length || _depths[line] < 0)
            return null;
        string text = buffer.Lines[line].TrimStart();
        int depth = _depths[line] - (text.StartsWith('}') ? 1 : 0);
        // The one statement under a brace-less if, else, for or while sits a step in.
        if (!text.StartsWith('{') && !text.StartsWith('}') && line > 0 && UnbracedBody(buffer.Lines[line - 1]))
            depth++;
        return Math.Max(0, depth) * TextBuffer.Indent;
    }

    private static bool UnbracedBody(string previous)
    {
        string code = previous.Trim();
        int comment = code.IndexOf("//", StringComparison.Ordinal);
        if (comment >= 0)
            code = code[..comment].TrimEnd();
        if (code.EndsWith('{') || code.EndsWith(';') || code.EndsWith('}'))
            return false;
        return code is "else" || code.StartsWith("if ", StringComparison.Ordinal) || code.StartsWith("if(", StringComparison.Ordinal)
               || code.StartsWith("else ", StringComparison.Ordinal) || code.StartsWith("for ", StringComparison.Ordinal)
               || code.StartsWith("for(", StringComparison.Ordinal) || code.StartsWith("while", StringComparison.Ordinal);
    }

    private int[] Depths(IReadOnlyList<string> lines)
    {
        int[] depths = new int[lines.Count];
        List<Bracket> brackets = SlangBrackets.Find(lines, Tokens);
        int next = 0, braces = 0, groups = 0;
        for (int line = 0; line < lines.Count; line++)
        {
            depths[line] = groups > 0 ? -1 : braces;
            for (; next < brackets.Count && brackets[next].Line == line; next++)
            {
                switch (lines[line][brackets[next].Column])
                {
                    case '{': braces++; break;
                    case '}': braces = Math.Max(0, braces - 1); break;
                    case '(' or '[': groups++; break;
                    case ')' or ']': groups = Math.Max(0, groups - 1); break;
                }
            }
        }

        return depths;
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

    public bool TakeCtrlClick(TextBuffer buffer) => GoToDeclaration(buffer);

    public string LineComment => "//";

    /// <summary>Puts the caret at the start of the declaration of the name at (or just before) it; false when the code doesn't declare one.</summary>
    private bool GoToDeclaration(TextBuffer buffer)
    {
        (int Line, int Column)? found = SlangRename.Declaration(buffer.Lines, buffer.Line, buffer.Column);
        if (found is null && buffer.Column > 0)
            found = SlangRename.Declaration(buffer.Lines, buffer.Line, buffer.Column - 1);
        if (found is not { } at)
            return GoElsewhere?.Invoke(buffer) == true;
        _rename = null;
        popup.Dismiss(buffer);
        buffer.Place(at.Line, at.Column);
        return true;
    }

    /// <summary>Starts renaming the name at (or just before) the caret, if the code declares it.</summary>
    private void StartRename(TextBuffer buffer)
    {
        _rename = null;
        if (ReadOnly)
            return;
        (string Name, List<(int Line, int Column)> Places)? found = SlangRename.Find(buffer.Lines, buffer.Line, buffer.Column);
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
