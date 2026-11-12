using System;
using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Ui.Editing;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Slang;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// What a name is, in a box under it, once the pointer has rested on it a
/// moment: why it is faded if it is, its signature (each overload's) and
/// what its <c>///</c> comment says. Goes the moment the pointer leaves the name, a key edits or a button is
/// down, and never shows while the completion list is open.
/// </summary>
internal sealed class HoverPopup
{
    private const float Delay = 0.4f;
    private const float MaxWidth = 540;
    private const float Pad = 8;
    private const float Row = 18;
    private const int TextSize = 13;

    private static readonly Color Signature = new("#82aaff");

    private readonly IRectCanvas _canvas;
    private readonly Handle _item;
    /// <summary>The code's area: the box stays inside it. Follows the editor when it is resized.</summary>
    public Rect2 Bounds { get; set; }

    /// <summary>One character's width at <see cref="TextSize"/>.</summary>
    private readonly float _charWidth;

    /// <summary>The name the pointer is on (line, where it starts), and how long it has been there.</summary>
    private (int Line, int Start)? _over;
    private float _rested;
    private List<Completion>? _shown;
    private string? _shownReason;
    private Vector2 _anchor;
    private float _lineHeight;

    public HoverPopup(IRectCanvas canvas, Handle item, Rect2 bounds)
    {
        _canvas = canvas;
        _item = item;
        Bounds = bounds;
        _charWidth = UiTextMetrics.MonoCharWidth(TextSize);
    }

    /// <summary>
    /// Follows the pointer: <paramref name="under"/> is the code under it (null when it is
    /// off the code, or when the box mustn't show: typing, a press, the completion list).
    /// </summary>
    /// <param name="faded">Why the name at a place is faded, or null.</param>
    public void Follow(
        TextBuffer buffer, SlangCompletions completions, Func<int, int, string?> faded,
        (int Line, int Column, float Bottom)? under, Vector2 pointer, float delta)
    {
        List<Completion>? found = null;
        string? reason = null;
        (int, int)? over = null;
        if (under is { } at)
        {
            found = completions.Describe(buffer.Lines[at.Line], at.Column, buffer.Lines, out int start);
            reason = faded(at.Line, at.Column);
            if (found is null && reason is not null)
            {
                start = at.Column;
                string text = buffer.Lines[at.Line];
                while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
                    start--;
            }

            if (found is not null || reason is not null)
                over = (at.Line, start);
        }

        if (over != _over)
        {
            _over = over;
            _rested = 0;
            _shown = null;
            _shownReason = null;
            return;
        }

        if (over is null || under is not { } still)
            return;
        _rested += delta;
        if (_shown is null && _rested >= Delay)
        {
            _shown = found ?? [];
            _shownReason = reason;
            _anchor = new Vector2(pointer.X, still.Bottom);
            _lineHeight = TextEditorLayout.LineHeight;
        }
    }

    public void Draw()
    {
        _canvas.Clear(_item);
        if (_shown is null)
            return;

        int fits = (int)((MaxWidth - Pad * 2) / _charWidth);
        var rows = new List<(string Text, Color Colour)>();
        void Doc(string doc)
        {
            rows.Add(("", Palette.Text));
            foreach (string paragraph in doc.Split('\n'))
            {
                bool code = paragraph.StartsWith("    ", StringComparison.Ordinal);
                foreach (string piece in code ? [paragraph[2..]] : Wrap(paragraph, fits))
                    rows.Add((piece, code ? Palette.TextDim : Palette.Text));
            }
        }

        if (_shownReason is { } why)
        {
            foreach (string piece in Wrap(char.ToUpperInvariant(why[0]) + why[1..] + ".", fits))
                rows.Add((piece, Palette.Warning));
            if (_shown.Count > 0)
                rows.Add(("", Palette.Text));
        }

        // Overloads sharing one comment list their signatures, then it once; otherwise each signature has its own.
        bool shared = _shown.Count > 0 && _shown.TrueForAll(entry => entry.Doc == _shown[0].Doc);
        for (int e = 0; e < _shown.Count; e++)
        {
            if (e > 0 && !shared)
                rows.Add(("", Palette.Text));
            foreach (string piece in Wrap(_shown[e].Header, fits, "    "))
                rows.Add((piece, Signature));
            if (_shown[e].Doc is { } doc && (!shared || e == _shown.Count - 1))
                Doc(doc);
        }

        int widest = 0;
        foreach ((string text, _) in rows)
            widest = Math.Max(widest, text.Length);
        float width = Math.Min(MaxWidth, widest * _charWidth + Pad * 2 + 4);
        float height = rows.Count * Row + Pad;
        float x = Math.Clamp(_anchor.X - 12, Bounds.Position.X, Bounds.End.X - width);
        float y = _anchor.Y + 2;
        if (y + height > Bounds.End.Y)
            y = _anchor.Y - _lineHeight - height - 2;
        var box = new Rect2(x, y, width, height);

        _canvas.DrawRect(_item, new Rect2(box.Position - Vector2.One, box.Size + Vector2.One * 2), CompletionPopup.Border);
        _canvas.DrawRect(_item, box, CompletionPopup.Back);
        for (int r = 0; r < rows.Count; r++)
        {
            if (rows[r].Text.Length == 0)
            {
                if (r > 0 && rows[r - 1].Colour == Signature)
                    _canvas.DrawRect(_item, new Rect2(x + Pad, y + Pad / 2 + r * Row + Row / 2, width - Pad * 2, 1), CompletionPopup.Border);
                continue;
            }

            _canvas.DrawTextMono(
                _item, new Rect2(x + Pad - 4, y + Pad / 2 + r * Row, width - Pad, Row), rows[r].Text, rows[r].Colour, TextSize, TextAlign.Left);
        }
    }

    /// <summary>Text broken at spaces into rows of at most <paramref name="fits"/> characters, rows after the first led by <paramref name="indent"/>.</summary>
    private static List<string> Wrap(string text, int fits, string indent = "")
    {
        var rows = new List<string>();
        while (text.Length > fits)
        {
            // A space inside the indent is no place to break: cutting there would leave the text as it was, forever.
            int cut = text.LastIndexOf(' ', fits);
            if (cut <= indent.Length)
                cut = fits;
            rows.Add(text[..cut].TrimEnd());
            text = indent + text[cut..].TrimStart();
        }

        rows.Add(text);
        return rows;
    }
}
