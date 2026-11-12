using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Systems.Canvas;
using Xanadu.Ui.Editing;

namespace Xanadu.Ecs;

/// <summary>
/// A multi-line text editor: the text, its undo, where it draws, and what its
/// systems keep between frames. It knows nothing about what the text is; an
/// owner that does (a code editor) colours it, adds marks and takes keys first
/// through <see cref="Host"/>. <see cref="TextEditors.Create"/> makes one.
/// </summary>
/// <remarks>
/// <c>TextEditorInputSystem</c> turns keys, typing and the pointer into edits
/// for the focused one (<see cref="TextEditorFocused"/>); <c>TextEditorDrawSystem</c>
/// draws every one. Owners read <see cref="Buffer"/> and <see cref="Frame"/> after
/// the systems have run.
/// </remarks>
public struct TextEditor : IComponent
{
    public TextBuffer Buffer;
    public TextHistory History;

    /// <summary>Colours, marks and first look at keys and presses; null for plain text.</summary>
    public ITextEditorHost? Host;

    /// <summary>
    /// The caret moves, selects and copies, but nothing edits: typing, cutting, pasting, undo and the
    /// editing keys are refused (<see cref="TextEditorFrame.Refused"/>), and the host never sees them.
    /// </summary>
    public bool ReadOnly;

    /// <summary>The canvas item it draws into, clipped to its area.</summary>
    public Handle Item;

    /// <summary>Its area, scroll and the rows drawn last frame: where things are on screen.</summary>
    public TextEditorLayout Layout;

    /// <summary>What the input system did this frame.</summary>
    public TextEditorFrame Frame;

    /// <summary>Seconds since the caret last moved or the text changed: solid while moving, blinking once still.</summary>
    public float SinceCaretMove;

    /// <summary>A press began in the text and is still held: moving drags the selection.</summary>
    public bool Dragging;

    /// <summary>When and where the last click landed, for a double click.</summary>
    public float LastClick;
    public (int Line, int Column) LastClickAt;
}

/// <summary>The text editor that keys and typing go to.</summary>
public struct TextEditorFocused : ITag;

/// <summary>What happened to a text editor in the input system this frame, for its owner; <see cref="Refused"/>: an edit was tried on a read-only one.</summary>
public readonly record struct TextEditorFrame(bool Edited, bool CaretMoved, bool SteppedHistory, bool Refused = false);

/// <summary>A place in the text.</summary>
public readonly record struct TextCell(int Line, int Column);

/// <summary>Columns [<see cref="Start"/>, <see cref="End"/>) of a line drawn in a colour; text no run covers is plain.</summary>
public readonly record struct TextRun(int Start, int End, Color Colour);

/// <summary>A label drawn in front of a column that isn't in the text (a parameter name); the caret steps over it.</summary>
public readonly record struct TextInlay(int Column, string Label);

/// <summary>
/// A line marked for attention: tinted, the word from <see cref="Underline"/>
/// underlined (-1 for none), and a note on a row of its own under it.
/// </summary>
public readonly record struct TextLineMark(Color Tint, Color Colour, int Underline = -1, string? Note = null);

/// <summary>What an owner adds to one line as it is drawn. Reused from line to line.</summary>
public sealed class TextLineDecor
{
    /// <summary>Colour runs in column order, not overlapping.</summary>
    public readonly List<TextRun> Runs = new();

    /// <summary>Inlays in column order.</summary>
    public readonly List<TextInlay> Inlays = new();

    public TextLineMark? Mark;

    public void Clear()
    {
        Runs.Clear();
        Inlays.Clear();
        Mark = null;
    }
}

/// <summary>
/// What a text editor's owner adds on top of plain text. Every member has a
/// default, so an owner implements only what it needs.
/// </summary>
public interface ITextEditorHost
{
    /// <summary>Once a frame before drawing: work done for the whole text, and cells to box (a matching bracket pair).</summary>
    void Prepare(TextBuffer buffer, List<TextCell> boxed)
    {
    }

    /// <summary>One line's colours, inlays and mark. Called for lines as they are drawn or hit, so keep it cheap.</summary>
    void Decorate(TextBuffer buffer, int line, TextLineDecor decor)
    {
    }

    /// <summary>A key press (or a held key repeating) before the editor's own handling; true when taken.</summary>
    bool TakeKey(TextBuffer buffer, Key key, bool shift, bool ctrl) => false;

    /// <summary>Characters typed, before they are inserted; true when the owner put them in itself.</summary>
    bool TakeTyped(TextBuffer buffer, string text) => false;

    /// <summary>A press inside the editor's area before it places the caret (a popup over the text); true when taken.</summary>
    bool TakePress(TextBuffer buffer, Vector2 point) => false;

    /// <summary>A Ctrl+click, once the caret is placed at it (go to what is there); true when taken.</summary>
    bool TakeCtrlClick(TextBuffer buffer) => false;

    /// <summary>What starts a line comment in this text (<c>//</c>), for Ctrl+/; null where there are none.</summary>
    string? LineComment => null;
}

/// <summary>
/// Where a text editor's text sits on screen: its area, the first line shown,
/// and the rows drawn last frame (a line, or the note under a marked line).
/// Turns points into places in the text and back.
/// </summary>
public sealed class TextEditorLayout
{
    public const int FontSize = 14;
    public const float LineHeight = 20;
    public const float Gutter = 44;
    public const float Inset = 10;

    /// <summary>The canvas's text sits this far right of its box's left edge.</summary>
    public const float TextPadding = 4;

    private readonly TextLineDecor _scratch = new();

    public TextEditorLayout(Rect2 area)
    {
        Area = area;
        CharWidth = UiTextMetrics.MonoCharWidth(FontSize);
    }

    public Rect2 Area { get; set; }

    /// <summary>One character's width in the monospaced face.</summary>
    public float CharWidth { get; }

    /// <summary>The first line shown.</summary>
    public int Scroll { get; set; }

    /// <summary>Where the caret was drawn last frame (blinking or not); null when its line is scrolled away.</summary>
    public Rect2? CaretRect { get; internal set; }

    /// <summary>Rows that fit: a line takes one, a note one more.</summary>
    public int VisibleRows => (int)((Area.Size.Y - Inset * 2) / LineHeight);

    public float TextLeft => Area.Position.X + Gutter + Inset;

    /// <summary>The rows drawn last frame, top to bottom: their top, their line, and for a note row the marked column.</summary>
    internal List<(float Top, int Line, int? NoteColumn)> Rows { get; } = new();

    /// <summary>
    /// The line and column under a point (a note row gives its mark's place), or null outside the text.
    /// While dragging a point outside still counts: above or below it gives the line just past the edge,
    /// so holding it there scrolls.
    /// </summary>
    public (int Line, int Column)? Hit(TextBuffer buffer, ITextEditorHost? host, Vector2 point, bool dragging = false)
    {
        if (Rows.Count == 0 || (!dragging && !Area.HasPoint(point)))
            return null;

        if (dragging)
        {
            int edge = point.Y < Area.Position.Y ? Math.Max(0, Rows[0].Line - 1)
                : point.Y >= Area.End.Y ? Math.Min(buffer.Lines.Count - 1, Rows[^1].Line + 1)
                : -1;
            if (edge >= 0)
                return (edge, Column(buffer, host, edge, point.X));
        }

        var row = Rows[^1];
        foreach (var candidate in Rows)
        {
            if (point.Y < candidate.Top + LineHeight)
            {
                row = candidate;
                break;
            }
        }

        if (row.NoteColumn is { } column && !dragging)
            return (row.Line, Math.Max(0, column));

        return (row.Line, Column(buffer, host, row.Line, point.X));
    }

    /// <summary>The character under a point and the bottom of its row; null off the text, past a line's end, on an inlay or a note.</summary>
    public (int Line, int Column, float Bottom)? Under(TextBuffer buffer, ITextEditorHost? host, Vector2 point)
    {
        if (!Area.HasPoint(point) || point.X < TextLeft)
            return null;
        foreach ((float top, int index, int? note) in Rows)
        {
            if (point.Y < top || point.Y >= top + LineHeight)
                continue;
            if (note is not null || index >= buffer.Lines.Count)
                return null;
            string line = buffer.Lines[index];
            int visual = (int)((point.X - TextLeft) / CharWidth), shift = 0;
            foreach (TextInlay inlay in Decorate(buffer, host, index).Inlays)
            {
                int at = inlay.Column + shift;
                if (visual < at)
                    break;
                if (visual < at + inlay.Label.Length + 1)
                    return null;
                shift += inlay.Label.Length + 1;
            }

            int column = visual - shift;
            return column < line.Length ? (index, column, top + LineHeight) : null;
        }

        return null;
    }

    /// <summary>Scrolls just enough to keep a line, and its note if it has one, in view.</summary>
    public void Reveal(TextBuffer buffer, ITextEditorHost? host, int line)
    {
        if (line < Scroll)
        {
            Scroll = line;
            return;
        }

        while (Scroll < line && RowsFrom(buffer, host, Scroll, line) > VisibleRows)
            Scroll++;
    }

    /// <summary>Where a text column is drawn: inlays at or before it push it right, so the caret sits after one.</summary>
    public static int VisualColumn(List<TextInlay> inlays, int column)
    {
        int visual = column;
        foreach (TextInlay inlay in inlays)
        {
            if (inlay.Column <= column)
                visual += inlay.Label.Length + 1;
        }

        return visual;
    }

    /// <summary>The owner's decoration of one line, in a scratch object that the next call reuses.</summary>
    internal TextLineDecor Decorate(TextBuffer buffer, ITextEditorHost? host, int line)
    {
        _scratch.Clear();
        host?.Decorate(buffer, line, _scratch);
        return _scratch;
    }

    private int Column(TextBuffer buffer, ITextEditorHost? host, int line, float x)
    {
        int visual = Math.Max(0, (int)MathF.Round((x - TextLeft) / CharWidth));
        return LogicalColumn(Decorate(buffer, host, line).Inlays, visual, buffer.Lines[line].Length);
    }

    private int RowsFrom(TextBuffer buffer, ITextEditorHost? host, int first, int last)
    {
        int rows = last - first + 1;
        if (host is null)
            return rows;
        for (int line = first; line <= last && line < buffer.Lines.Count; line++)
        {
            if (Decorate(buffer, host, line).Mark?.Note is not null)
                rows++;
        }

        return rows;
    }

    /// <summary>The text column drawn at a visual column; a click on an inlay lands just after it.</summary>
    private static int LogicalColumn(List<TextInlay> inlays, int visual, int length)
    {
        int shift = 0;
        foreach (TextInlay inlay in inlays)
        {
            int at = inlay.Column + shift;
            if (visual < at)
                break;
            if (visual <= at + inlay.Label.Length + 1)
                return inlay.Column;
            shift += inlay.Label.Length + 1;
        }

        return Math.Clamp(visual - shift, 0, length);
    }
}

/// <summary>Making and focusing text editors.</summary>
public static class TextEditors
{
    /// <summary>A text editor over <paramref name="area"/>, drawing into a new canvas item under <paramref name="parent"/>, focused.</summary>
    public static Entity Create(EntityStore store, Handle parent, Rect2 area, string text, ITextEditorHost? host = null)
    {
        Handle item = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(item, parent);
        CanvasServer.Current.SetCustomRect(item, true, area);
        CanvasServer.Current.SetClip(item, true);

        var buffer = new TextBuffer();
        buffer.Load(text);
        Entity entity = store.CreateEntity(new TextEditor
        {
            Buffer = buffer,
            History = new TextHistory(buffer),
            Host = host,
            Item = item,
            Layout = new TextEditorLayout(area),
            LastClick = -1,
            LastClickAt = (-1, -1),
        });
        entity.AddTag<TextEditorFocused>();
        return entity;
    }

    /// <summary>Removes a text editor and its canvas item.</summary>
    public static void Delete(Entity entity)
    {
        if (entity.IsNull)
            return;
        CanvasServer.Current.Free(entity.GetComponent<TextEditor>().Item);
        entity.DeleteEntity();
    }
}
