using System;
using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>One tab as the strip shows it.</summary>
internal readonly record struct TabLook(string Label, bool Dirty, bool ReadOnly, bool Closes);

/// <summary>A question over the code that has to be answered before going on.</summary>
internal sealed record EditorQuestion(string Title, string Message, string Yes, Action OnYes);

/// <summary>
/// The strip of open texts over the code (the shader first, then library
/// modules gone to), each with its close cross, and the box that asks before
/// something lasting happens (editing the library, dropping unsaved changes).
/// The open tab is lit; one that can't be edited says so at the strip's end.
/// </summary>
internal sealed class EditorTabs(IRectCanvas canvas, Handle item)
{
    public const float Height = 28;
    private const int TextSize = 13;
    private const float Pad = 10;
    private const float Cross = 18;
    private const float QuestionWidth = 440, QuestionHeight = 132;
    private const float ButtonWidth = 130, ButtonHeight = 30;

    private static readonly Color Strip = new("#0a0f1d");

    private readonly float _charWidth = UiTextMetrics.MonoCharWidth(TextSize);
    private readonly List<(Rect2 Box, Rect2 Cross)> _boxes = new();
    private Rect2 _yes, _no;

    /// <summary>The strip's box, over the code.</summary>
    public Rect2 Area { get; set; }

    /// <summary>The code's box: the question sits in its middle.</summary>
    public Rect2 Code { get; set; }

    public EditorQuestion? Question { get; set; }

    public void Draw(IReadOnlyList<TabLook> tabs, int open, Vector2 pointer)
    {
        canvas.Clear(item);
        canvas.DrawRect(item, Area, Strip);
        _boxes.Clear();
        float x = Area.Position.X;
        for (int i = 0; i < tabs.Count; i++)
        {
            TabLook tab = tabs[i];
            string label = tab.Dirty ? tab.Label + " •" : tab.Label;
            float width = label.Length * _charWidth + Pad * 2 + (tab.Closes ? Cross : 0);
            var box = new Rect2(x, Area.Position.Y, width, Area.Size.Y);
            var cross = tab.Closes ? new Rect2(box.End.X - Pad / 2 - Cross, box.Position.Y, Cross, box.Size.Y) : default;
            _boxes.Add((box, cross));
            bool lit = i == open;
            canvas.DrawRect(item, box, lit ? Palette.SurfaceRaised : box.HasPoint(pointer) ? Palette.SurfaceDeep : Strip);
            if (lit)
                canvas.DrawRect(item, new Rect2(box.Position, new Vector2(box.Size.X, 2)), Palette.PrimarySoft);
            canvas.DrawTextMono(item, new Rect2(box.Position.X + Pad - 4, box.Position.Y, width, box.Size.Y), label,
                lit ? Palette.Text : Palette.TextDim, TextSize, TextAlign.Left);
            if (tab.Closes)
            {
                canvas.DrawTextMono(item, cross, "×", cross.HasPoint(pointer) ? Palette.Text : Palette.TextMuted.Lerp(Strip, 0.4f),
                    TextSize, TextAlign.Center);
            }

            x = box.End.X + 1;
        }

        if (open >= 0 && open < tabs.Count && tabs[open].ReadOnly)
        {
            var note = new Rect2(x + Pad, Area.Position.Y, Math.Max(0, Area.End.X - x - Pad * 2), Area.Size.Y);
            canvas.DrawTextStyledRight(item, note, "read-only library · type to unlock", Palette.TextMuted, 13);
        }

        DrawQuestion(pointer);
    }

    private void DrawQuestion(Vector2 pointer)
    {
        if (Question is not { } question)
            return;
        Vector2 middle = Code.GetCenter();
        var box = new Rect2(middle.X - QuestionWidth / 2, middle.Y - QuestionHeight / 2, QuestionWidth, QuestionHeight);
        canvas.DrawRect(item, Code, new Color(0, 0, 0, 0.35f));
        canvas.DrawRect(item, new Rect2(box.Position - Vector2.One, box.Size + Vector2.One * 2), CompletionPopup.Border);
        canvas.DrawRect(item, box, CompletionPopup.Back);
        canvas.DrawTextStyledLeft(item, new Rect2(box.Position.X + 16, box.Position.Y + 12, box.Size.X - 32, 22), question.Title, Palette.Text, 15);
        canvas.DrawTextStyledLeft(item, new Rect2(box.Position.X + 16, box.Position.Y + 38, box.Size.X - 32, 22), question.Message, Palette.TextDim, 13);

        float top = box.End.Y - 16 - ButtonHeight;
        _no = new Rect2(box.End.X - 16 - ButtonWidth, top, ButtonWidth, ButtonHeight);
        _yes = new Rect2(_no.Position.X - 8 - ButtonWidth, top, ButtonWidth, ButtonHeight);
        Button(_yes, question.Yes, Palette.PrimaryMuted, pointer);
        Button(_no, "CANCEL", Palette.SurfaceAlt, pointer);
    }

    private void Button(Rect2 box, string label, Color fill, Vector2 pointer)
    {
        canvas.DrawRect(item, box, box.HasPoint(pointer) ? fill.Lerp(Colors.White, 0.12f) : fill);
        canvas.DrawTextStyled(item, box, label, Palette.Text, 13);
    }

    /// <summary>The tab under a point, and whether it is on its cross; null off the tabs.</summary>
    public (int Tab, bool Close)? TabAt(Vector2 point)
    {
        for (int i = 0; i < _boxes.Count; i++)
        {
            if (_boxes[i].Box.HasPoint(point))
                return (i, _boxes[i].Cross.HasPoint(point));
        }

        return null;
    }

    /// <summary>A press while asking: true on yes, false on cancel, null elsewhere.</summary>
    public bool? AnswerAt(Vector2 point) => _yes.HasPoint(point) ? true : _no.HasPoint(point) ? false : null;
}
