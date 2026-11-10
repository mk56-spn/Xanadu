using System;
using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Labs.Editor;

/// <summary>
/// The completion list under the caret. It opens when typing leaves the caret
/// after a dot it has something for, narrows as you type, and closes when the
/// caret leaves that spot or on Esc (until the spot changes). Up/Down choose,
/// Tab/Enter or a click take the choice.
/// </summary>
internal sealed class CompletionPopup
{
    private const int Shown = 10;
    private const float Row = 22;
    private const float Width = 460;
    private const int TextSize = 13;

    private static readonly Color Back = new("#161e38");
    private static readonly Color Border = new("#2c3a66");
    private static readonly Color Chosen = new("#24335f");

    private readonly IRectCanvas _canvas;
    private readonly Handle _item;
    private readonly Rect2 _bounds;
    private List<Completion> _items = [];
    private int _selected;
    private int _first;
    private Rect2 _box;

    /// <summary>The (line, column) it was closed at with Esc; it stays shut there.</summary>
    private (int Line, int Start)? _dismissed;

    public CompletionPopup(IRectCanvas canvas, Handle item, Rect2 bounds)
    {
        _canvas = canvas;
        _item = item;
        _bounds = bounds;
    }

    public bool Open { get; private set; }

    /// <summary>Where the typed part starts: what a choice replaces up to the caret.</summary>
    public int Start { get; private set; }

    /// <summary>Follows the caret: opens after an edit when there is something to offer, closes when there isn't.</summary>
    public void Follow(CodeBuffer buffer, SlangCompletions completions, bool edited)
    {
        string before = buffer.Lines[buffer.Line][..buffer.Column];
        List<Completion>? found = completions.At(before, buffer.Lines, out int start);
        if (found is null || _dismissed == (buffer.Line, start))
        {
            Open = false;
            if (found is null)
                _dismissed = null;
            return;
        }

        if (!Open && !edited)
            return;

        if (!Open || start != Start)
            _selected = 0;
        Open = true;
        Start = start;
        _items = found;
        _selected = Math.Clamp(_selected, 0, _items.Count - 1);
    }

    public void Move(int by)
    {
        _selected = (_selected + by + _items.Count) % _items.Count;
    }

    public void Dismiss(CodeBuffer buffer)
    {
        Open = false;
        _dismissed = (buffer.Line, Start);
    }

    /// <summary>Takes the chosen entry: the typed part becomes its name, and a function gets its opening bracket.</summary>
    public void Accept(CodeBuffer buffer)
    {
        if (!Open)
            return;
        Completion choice = _items[_selected];
        bool call = choice.Kind is CompletionKind.Function or CompletionKind.Method;
        buffer.Replace(Start, buffer.Column, choice.Name + (call ? "(" : ""));
        Open = false;
    }

    /// <summary>Takes the entry under a click; true if the click was on the list.</summary>
    public bool Click(CodeBuffer buffer, Vector2 point)
    {
        if (!Open || !_box.HasPoint(point))
            return false;
        int row = (int)((point.Y - _box.Position.Y - 2) / Row);
        if (row >= 0 && _first + row < _items.Count)
        {
            _selected = _first + row;
            Accept(buffer);
        }

        return true;
    }

    /// <summary>Draws under the caret (above it when there's no room below).</summary>
    public void Draw(Rect2? caret)
    {
        _canvas.Clear(_item);
        if (!Open || caret is not { } at)
            return;

        int rows = Math.Min(Shown, _items.Count);
        _first = Math.Clamp(_first, Math.Max(0, _selected - rows + 1), _selected);
        float height = rows * Row + 4;
        float y = at.End.Y + 2;
        if (y + height > _bounds.End.Y)
            y = at.Position.Y - height - 2;
        float x = Math.Min(at.Position.X, _bounds.End.X - Width);
        _box = new Rect2(x, y, Width, height);

        _canvas.DrawRect(_item, new Rect2(_box.Position - Vector2.One, _box.Size + Vector2.One * 2), Border);
        _canvas.DrawRect(_item, _box, Back);
        for (int r = 0; r < rows; r++)
        {
            Completion entry = _items[_first + r];
            float top = y + 2 + r * Row;
            if (_first + r == _selected)
                _canvas.DrawRect(_item, new Rect2(x, top, Width, Row), Chosen);

            float left = x + 8;
            if (entry.Swatch is { } s)
                _canvas.DrawRect(_item, new Rect2(left, top + 5, 12, 12), new Color(s.X, s.Y, s.Z, 1));
            else
                _canvas.DrawTextMono(_item, new Rect2(left - 4, top, 20, Row), Mark(entry.Kind), Palette.TextMuted, 11, TextAlign.Left);
            _canvas.DrawTextMono(_item, new Rect2(left + 18, top, Width * 0.5f, Row), entry.Name, Palette.Text, TextSize, TextAlign.Left);
            _canvas.DrawTextMono(
                _item, new Rect2(x + Width * 0.45f, top, Width * 0.55f - 8, Row), Clip(entry.Detail, 34),
                Palette.TextMuted, 12, TextAlign.Right);
        }

        if (_items.Count > rows)
        {
            float track = height - 4;
            float thumb = track * rows / _items.Count;
            float offset = (track - thumb) * _first / (_items.Count - rows);
            _canvas.DrawRect(_item, new Rect2(x + Width - 4, y + 2 + offset, 3, thumb), Border);
        }
    }

    private static string Mark(CompletionKind kind) => kind switch
    {
        CompletionKind.Field => "f",
        CompletionKind.Method => "m",
        CompletionKind.Function => "ƒ",
        CompletionKind.Constant => "c",
        CompletionKind.Type => "T",
        _ => "e",
    };

    private static string Clip(string text, int length) => text.Length > length ? text[..(length - 1)] + "…" : text;
}
