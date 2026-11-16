using System;
using System.Collections.Generic;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Ui.Editing;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The code's points as dots on the preview, to drag. The <c>float2(x, y)</c> the caret is on,
/// when it's a place (<see cref="SlangPoints"/>), shows where it lands, in pixels from the
/// preview's centre as <c>canvas.centred</c> counts them; dragging it writes the new numbers into
/// the text, whole pixels, the drag one undo step. The <c>uniform float2</c>s that read as places
/// are dots too, always shown, and dragging one sets its value as its sliders do.
/// A point off the preview sits on its edge, hollow.
/// </summary>
internal sealed class PreviewPoints
{
    private const float Radius = 6;
    /// <summary>How near a press has to be to a dot to take it.</summary>
    private const float Reach = 12;
    private const int TextSize = 12;

    /// <summary>The caret's point, worked out again each frame from the text.</summary>
    private PointLiteral? _caret;

    /// <summary>The point in the text being dragged, and where on its dot it was taken.</summary>
    private (PointLiteral Point, Vector2 Grip)? _text;

    /// <summary>The uniform being dragged, and where on its dot it was taken.</summary>
    private (string Name, Vector2 Grip)? _uniform;

    public bool Dragging => _text is not null || _uniform is not null;

    /// <summary>Finds the point under the caret: the shader's own text only, never while a library tab is open.</summary>
    public void Follow(TextBuffer? buffer, Func<string, int, IReadOnlyList<string>> parameterNames)
    {
        if (_text is { } dragged)
        {
            _caret = dragged.Point;
            return;
        }

        _caret = buffer is null ? null : SlangPoints.At(buffer.Lines, buffer.Line, buffer.Column, parameterNames);
    }

    /// <summary>
    /// Starts, continues or ends a drag; true while one is held (the pointer is its). On letting go
    /// the history is let go and <paramref name="done"/> runs (the code takes its focus back).
    /// </summary>
    public bool Point(
        TextBuffer buffer, TextHistory history, Func<string, int, IReadOnlyList<string>> parameterNames,
        Rect2 preview, EditorTuning tuning, Vector2 mouse, bool held, bool pressed, Action done)
    {
        Vector2 centre = preview.GetCenter();
        if (!held)
        {
            if (_text is not null)
                history.LetGo();
            bool was = Dragging;
            (_text, _uniform) = (null, null);
            if (was)
                done();
            return false;
        }

        if (pressed && !Dragging && preview.HasPoint(mouse))
        {
            // The caret's point first: it's the one being looked at.
            if (_caret is { } point && Near(mouse, On(preview, centre + point.Value)))
            {
                _text = (point, mouse - (centre + point.Value));
                history.Hold();
            }
            else
            {
                foreach ((string name, Vector2 value) in tuning.Points())
                {
                    if (Near(mouse, On(preview, centre + value)))
                    {
                        _uniform = (name, mouse - (centre + value));
                        break;
                    }
                }
            }
        }

        if (_text is { } text)
        {
            Vector2 wanted = mouse - text.Grip - centre;
            string written = SlangPoints.Text(wanted);
            PointLiteral now = text.Point;
            if (buffer.Lines[now.Line][now.Start..now.End] != written)
            {
                // The text under it may have changed some other way: only a point still there is written.
                if (SlangPoints.At(buffer.Lines, now.Line, now.Start, parameterNames) is not { } still || still.Start != now.Start)
                {
                    history.LetGo();
                    _text = null;
                    done();
                    return false;
                }

                buffer.Edit(now.Line, now.Start, still.End, written);
                var value = new Vector2(MathF.Round(wanted.X), MathF.Round(wanted.Y));
                _text = (now with { End = now.Start + written.Length, Value = value }, text.Grip);
                _caret = _text.Value.Point;
            }

            return true;
        }

        if (_uniform is { } uniform)
        {
            Vector2 wanted = mouse - uniform.Grip - centre;
            tuning.SetPoint(uniform.Name, new Vector2(MathF.Round(wanted.X), MathF.Round(wanted.Y)));
            return true;
        }

        return false;
    }

    public void Draw(IRectCanvas canvas, Handle item, Rect2 preview, EditorTuning tuning, Vector2 mouse)
    {
        Vector2 centre = preview.GetCenter();
        foreach ((string name, Vector2 value) in tuning.Points())
            DrawDot(canvas, item, preview, centre + value, name, value, _uniform?.Name == name, mouse);
        if (_caret is { } point)
            DrawDot(canvas, item, preview, centre + point.Value, point.Name, point.Value, _text is not null, mouse);
    }

    /// <summary>
    /// One point: muted blue at rest, bright blue while held (bright blue means on), a ring when
    /// the pointer could take it; its name and value beside it.
    /// </summary>
    private void DrawDot(IRectCanvas canvas, Handle item, Rect2 preview, Vector2 at, string name, Vector2 value, bool held, Vector2 mouse)
    {
        Vector2 shown = On(preview, at);
        bool off = shown != at;
        bool lit = held || (!Dragging && Near(mouse, shown));
        Color fill = held ? Palette.Primary : Palette.PrimaryMuted;
        if (!off)
            canvas.DrawCircle(item, shown, Radius, fill);
        canvas.DrawCircleOutline(item, shown, Radius, lit ? Palette.TextBright : Palette.PrimarySoft, off ? 2 : 1.5f);

        string label = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{name}  {value.X:0.#}, {value.Y:0.#}");
        // Beside it, flipped to its left near the preview's right edge.
        float width = label.Length * 6.2f;
        bool left = shown.X + Radius + 6 + width > preview.End.X;
        var box = new Rect2(left ? shown.X - Radius - 6 - width : shown.X + Radius + 6, shown.Y - 9, width, 18);
        canvas.DrawRect(item, new Rect2(box.Position.X - 3, box.Position.Y, width + 6, box.Size.Y), Palette.Background with { A = 0.75f });
        canvas.DrawTextStyledLeft(item, box, label, lit ? Palette.TextBright : Palette.Text, TextSize);
    }

    /// <summary>A point kept inside the preview, so one off it still shows on its edge.</summary>
    private static Vector2 On(Rect2 preview, Vector2 at) =>
        Vector2.Clamp(at, preview.Position + new Vector2(Radius), preview.End - new Vector2(Radius));

    private static bool Near(Vector2 mouse, Vector2 dot) => Vector2.DistanceSquared(mouse, dot) <= Reach * Reach;
}
