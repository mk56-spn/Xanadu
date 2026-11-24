using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Ui.Ecs.Ui.TextEditorLayout;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// Draws every <see cref="TextEditor"/> in the monospaced face: line numbers,
/// the caret's line, the selection behind the text, the caret, and what the
/// owner adds (<see cref="ITextEditorHost"/>): coloured runs, inlays as small
/// pills, boxed cells, and marked lines (tinted, a word underlined, a note on a
/// row of its own under the line, so it is never cut off by a long line), and
/// a faint guide down each indent step (blank lines carry the guides of the
/// code around them).
/// Redrawn whole when anything it shows moved; only the visible rows are drawn.
/// An editor on a laid-out node takes this frame's box first (<see cref="TextEditors.Follow"/>):
/// drawing runs after layout, so the text never trails its node.
/// </summary>
public sealed class TextEditorDrawSystem(IRectCanvas canvas) : QuerySystem<TextEditor>
{
    private const int InlaySize = 11;

    private static readonly Color Background = Palette.Background;
    private static readonly Color GutterText = new("#4a5578");

    /// <summary>Plain text: anything no run colours.</summary>
    public static readonly Color Plain = new("#d8def0");

    private static readonly Color BoxFill = new(1, 1, 1, 0.10f);
    private static readonly Color InlayText = new("#93a4cf");
    private static readonly Color InlayPill = new(0.55f, 0.65f, 0.95f, 0.10f);
    private static readonly Color CaretLine = new(1, 1, 1, 0.04f);
    private static readonly Color SelectionFill = new(0.35f, 0.55f, 1f, 0.30f);
    private static readonly Color SwatchRing = new(1, 1, 1, 0.35f);
    private static readonly Color IndentGuide = new(1, 1, 1, 0.07f);
    private static readonly Color StrayIndent = new(0.98f, 0.57f, 0.24f, 0.22f);

    private readonly List<TextCell> _boxed = [];

    protected override void OnUpdate() => Query.EachEntity(new DrawEditors(this, Query.Store));

    private void Draw(ref TextEditor editor, bool focused)
    {
        TextBuffer buffer = editor.Buffer;
        TextEditorLayout layout = editor.Layout;
        Handle item = editor.Item;

        // Solid while typing or moving, blinking once still; hidden without focus.
        bool caretOn = focused && (editor.SinceCaretMove < 0.5f || (int)(editor.SinceCaretMove * 2) % 2 == 0);

        // The picture stays on the canvas until cleared: redraw only when something it shows moved.
        var drawn = new TextEditorDrawn(buffer, editor.Host, buffer.Version, buffer.Line, buffer.Column, buffer.Anchor, layout.Scroll, layout.Area,
            focused, caretOn, editor.Host?.Version ?? 0, item);
        if (layout.Drawn == drawn)
            return;

        layout.Drawn = drawn;
        canvas.Clear(item);
        canvas.DrawRect(item, layout.Area, Background);
        layout.Rows.Clear();
        layout.CaretRect = null;
        _boxed.Clear();
        editor.Host?.Prepare(buffer, _boxed);

        float charWidth = layout.CharWidth;
        float bottom = layout.Area.End.Y - Inset;
        float y = layout.Area.Position.Y + Inset;
        for (int index = layout.Scroll; index < buffer.Lines.Count && y + LineHeight <= bottom; index++)
        {
            string line = buffer.Lines[index];
            TextLineDecor decor = layout.Decorate(buffer, editor.Host, index);
            var row = new Rect2(layout.Area.Position.X, y, layout.Area.Size.X, LineHeight);

            if (decor.Mark is { } tinted)
                canvas.DrawRect(item, row, tinted.Tint);
            else if (index == buffer.Line && focused)
                canvas.DrawRect(item, row, CaretLine);

            if (buffer.HasSelection)
                DrawSelection(item, layout, buffer, index, decor.Inlays, y);

            int guides = GuideLevel(buffer, index);
            for (int step = 0; step < guides; step++)
                canvas.DrawRect(item, new Rect2(layout.TextLeft + VisualColumn(decor.Inlays, step * TextBuffer.Indent) * charWidth, y, 1, LineHeight), IndentGuide);
            // Off the steps: the stray part of the indent marked, clicked to put the text where it belongs.
            if (TextIndent.Misaligned(line))
            {
                float from = layout.TextLeft + VisualColumn(decor.Inlays, guides * TextBuffer.Indent) * charWidth;
                float to = layout.TextLeft + VisualColumn(decor.Inlays, TextIndent.Of(line)) * charWidth;
                canvas.DrawRect(item, new Rect2(from, y + 1, to - from, LineHeight - 2), StrayIndent);
                canvas.DrawRect(item, new Rect2(from, y, 1, LineHeight), Palette.Warning);
            }

            canvas.DrawTextMono(
                item, new Rect2(layout.Area.Position.X, y, Gutter - TextPadding, LineHeight), (index + 1).ToString(),
                index == buffer.Line ? Plain : GutterText, FontSize, TextAlign.Right);
            foreach (TextCell cell in _boxed)
            {
                if (cell.Line == index)
                    canvas.DrawRect(item, new Rect2(layout.TextLeft + VisualColumn(decor.Inlays, cell.Column) * charWidth, y + 1, charWidth, LineHeight - 2), BoxFill);
            }

            DrawLine(item, layout, line, decor, y);
            if (index == buffer.Line)
            {
                float x = layout.TextLeft + VisualColumn(decor.Inlays, buffer.Column) * charWidth;
                layout.CaretRect = new Rect2(x - 1, y, 2, LineHeight);
                if (caretOn)
                    canvas.DrawRect(item, new Rect2(x - 1, y + 2, 2, LineHeight - 4), Palette.Accent);
            }

            layout.Rows.Add((y, index, null));
            y += LineHeight;

            if (decor.Mark is { } mark)
            {
                if (mark.Underline >= 0)
                    Underline(item, layout, line, decor.Inlays, mark, y - LineHeight);
                if (mark.Note is { } note && y + LineHeight <= bottom)
                {
                    int indent = line.Length - line.TrimStart(' ').Length;
                    canvas.DrawRect(item, new Rect2(layout.Area.Position.X, y, layout.Area.Size.X, LineHeight), mark.Tint);
                    DrawAt(item, layout, "└ " + note, indent, y, mark.Colour);
                    layout.Rows.Add((y, index, Math.Max(0, mark.Underline)));
                    y += LineHeight;
                }
            }
        }
    }

    /// <summary>
    /// How many indent guides a line shows: its whole indent steps, so the text
    /// itself is never crossed. A blank line takes the shallower of the nearest
    /// non-blank lines above and below, so guides run through gaps in a block.
    /// </summary>
    private static int GuideLevel(TextBuffer buffer, int index)
    {
        int own = Indent(buffer.Lines[index]);
        if (own >= 0)
            return own / TextBuffer.Indent;

        int above = -1, below = -1;
        for (int i = index - 1; i >= 0 && above < 0; i--)
            above = Indent(buffer.Lines[i]);
        for (int i = index + 1; i < buffer.Lines.Count && below < 0; i++)
            below = Indent(buffer.Lines[i]);
        return Math.Max(0, Math.Min(above, below)) / TextBuffer.Indent;
    }

    /// <summary>Leading spaces of a line, or -1 for a blank one.</summary>
    private static int Indent(string line)
    {
        int spaces = 0;
        while (spaces < line.Length && line[spaces] == ' ')
            spaces++;
        return spaces == line.Length ? -1 : spaces;
    }

    /// <summary>The selected part of one line; a line the selection runs past shows one more column for its line break.</summary>
    private void DrawSelection(Handle item, TextEditorLayout layout, TextBuffer buffer, int index, List<TextInlay> inlays, float y)
    {
        var (start, end) = buffer.Selection;
        if (index < start.Line || index > end.Line)
            return;

        int from = index == start.Line ? start.Column : 0;
        int to = index == end.Line ? end.Column : buffer.Lines[index].Length;
        float x0 = layout.TextLeft + VisualColumn(inlays, from) * layout.CharWidth;
        // The end stops before an inlay sitting right at it, so the inlay isn't taken into the selection.
        int visualEnd = to;
        foreach (TextInlay inlay in inlays)
        {
            if (inlay.Column < to)
                visualEnd += inlay.Label.Length + 1;
        }

        float x1 = layout.TextLeft + visualEnd * layout.CharWidth + (index < end.Line ? layout.CharWidth : 0);
        if (x1 > x0)
            canvas.DrawRect(item, new Rect2(x0, y, x1 - x0, LineHeight), SelectionFill);
    }

    private void DrawLine(Handle item, TextEditorLayout layout, string line, TextLineDecor decor, float y)
    {
        int visual = 0, logical = 0;
        foreach (TextInlay inlay in decor.Inlays)
        {
            DrawText(item, layout, line, decor.Runs, logical, inlay.Column, visual, y);
            visual += inlay.Column - logical;
            logical = inlay.Column;
            if (inlay.Swatch is { } swatch)
                DrawSwatch(item, layout, swatch, visual, y);
            else
                DrawInlay(item, layout, inlay.Label, visual, y);
            visual += inlay.Label.Length + 1;
        }

        DrawText(item, layout, line, decor.Runs, logical, line.Length, visual, y);
    }

    /// <summary>An inlay in a smaller face on a faint pill, so it can't be read as text.</summary>
    private void DrawInlay(Handle item, TextEditorLayout layout, string label, int visualColumn, float y)
    {
        float charWidth = layout.CharWidth;
        // The inlay has label + 1 columns of room; its smaller text sits centred in a pill a little narrower.
        float room = (label.Length + 1) * charWidth;
        float textWidth = label.Length * charWidth * InlaySize / FontSize;
        float pill = textWidth + 8;
        float x = layout.TextLeft + visualColumn * charWidth + (room - pill) / 2 - charWidth / 2;
        canvas.DrawRect(item, new Rect2(x, y + 3, pill, LineHeight - 6), InlayPill);
        canvas.DrawTextMono(item, new Rect2(x + 4 - TextPadding, y, textWidth + TextPadding * 2 + 8, LineHeight), label, InlayText, InlaySize, TextAlign.Left);
    }

    /// <summary>A colour square in its one column, ringed so a dark or see-through colour still shows; alpha shows as the background through it.</summary>
    private void DrawSwatch(Handle item, TextEditorLayout layout, Color colour, int visualColumn, float y)
    {
        const float Size = 10;
        float x = layout.TextLeft + visualColumn * layout.CharWidth + (layout.CharWidth - Size) / 2;
        float top = y + (LineHeight - Size) / 2;
        canvas.DrawRect(item, new Rect2(x, top, Size, Size), SwatchRing);
        canvas.DrawRect(item, new Rect2(x + 1, top + 1, Size - 2, Size - 2), Background);
        canvas.DrawRect(item, new Rect2(x + 1, top + 1, Size - 2, Size - 2), colour);
    }

    /// <summary>Columns [from, to) of a line, each run in its colour and the rest plain, starting at a visual column.</summary>
    private void DrawText(Handle item, TextEditorLayout layout, string line, List<TextRun> runs, int from, int to, int visual, float y)
    {
        int at = from;
        foreach (TextRun run in runs)
        {
            int start = Math.Max(run.Start, from), end = Math.Min(run.End, to);
            if (end <= start)
                continue;
            if (start > at)
                DrawAt(item, layout, line[at..start], visual + at - from, y, Plain);
            DrawAt(item, layout, line[start..end], visual + start - from, y, run.Colour);
            at = end;
        }

        if (to > at)
            DrawAt(item, layout, line[at..to], visual + at - from, y, Plain);
    }

    private void DrawAt(Handle item, TextEditorLayout layout, string text, int visualColumn, float y, Color colour)
    {
        if (text.Trim().Length == 0)
            return;
        float x = layout.TextLeft + visualColumn * layout.CharWidth - TextPadding;
        canvas.DrawTextMono(item, new Rect2(x, y, text.Length * layout.CharWidth + TextPadding * 2 + 8, LineHeight), text, colour, FontSize, TextAlign.Left);
    }

    /// <summary>The word at a mark's column underlined.</summary>
    private void Underline(Handle item, TextEditorLayout layout, string line, List<TextInlay> inlays, TextLineMark mark, float y)
    {
        int start = Math.Clamp(mark.Underline, 0, line.Length);
        int end = start;
        while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '_'))
            end++;
        end = Math.Max(end, start + 1);
        float x0 = layout.TextLeft + VisualColumn(inlays, start) * layout.CharWidth;
        canvas.DrawRect(item, new Rect2(x0, y + LineHeight - 3, (end - start) * layout.CharWidth, 2), mark.Colour);
    }

    private readonly struct DrawEditors(TextEditorDrawSystem System, EntityStore Store) : IEachEntity<TextEditor>
    {
        public void Execute(ref TextEditor editor, int id)
        {
            Entity entity = Store.GetEntityById(id);
            TextEditors.Follow(entity, ref editor);
            System.Draw(ref editor, entity.Tags.Has<TextEditorFocused>());
        }
    }
}
