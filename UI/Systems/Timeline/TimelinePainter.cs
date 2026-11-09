using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Systems.Canvas;

namespace Xanadu.Timeline;

/// <summary>How each level of tick is drawn on a ruler: its height up from the ruler's foot, colour and width.</summary>
public readonly record struct TimelineTickLook(float Height, Color Color, float Width = 1f);

/// <summary>A ruler's look per tick level.</summary>
public readonly record struct TimelineRulerLook(
    TimelineTickLook Major,
    TimelineTickLook Half,
    TimelineTickLook Quarter,
    TimelineTickLook Minor)
{
    public TimelineTickLook For(TimelineTickLevel level) => level switch
    {
        TimelineTickLevel.Major => Major,
        TimelineTickLevel.Half => Half,
        TimelineTickLevel.Quarter => Quarter,
        _ => Minor,
    };

    /// <summary>The same look for everything between majors.</summary>
    public static TimelineRulerLook TwoLevel(TimelineTickLook major, TimelineTickLook minor) =>
        new(major, minor, minor, minor);
}

/// <summary>
/// Draws the parts every timeline has, the same way in each tool: the ruler's
/// ticks and labels, lines down through the rows, the playhead, key markers
/// and eased spans. Each tool still draws its own rows, labels and extras
/// around these; all of them place through a <see cref="TimelineAxis"/>.
/// </summary>
public readonly struct TimelinePainter(IRectCanvas canvas, Rid target)
{
    /// <summary>The key marker's size, so keys and pins look the same in every timeline.</summary>
    public const float KeyRadius = 6f;

    /// <summary>The playhead's line width and the side of the tab on its top.</summary>
    public const float PlayheadWidth = 2f;
    public const float PlayheadTab = 10f;

    /// <summary>Ticks rising from the foot of <paramref name="ruler"/>.</summary>
    public void RulerTicks(TimelineAxis axis, Rect2 ruler, IReadOnlyList<TimelineTick> ticks, TimelineRulerLook look)
    {
        foreach (TimelineTick tick in ticks)
        {
            if (!axis.Shows(tick.TimeMs))
                continue;

            TimelineTickLook part = look.For(tick.Level);
            float x = axis.ClampedX(tick.TimeMs);
            canvas.DrawLine(target, new Vector2(x, ruler.End.Y - part.Height), new Vector2(x, ruler.End.Y), part.Color, part.Width);
        }
    }

    /// <summary>The ticks' labels along the top of <paramref name="ruler"/>, just right of each tick; none that would run off its end.</summary>
    public void Labels(TimelineAxis axis, Rect2 ruler, IReadOnlyList<TimelineTick> ticks, Color color, int fontSize = 11)
    {
        foreach (TimelineTick tick in ticks)
        {
            if (tick.Label is not { } label || !axis.Shows(tick.TimeMs))
                continue;

            float x = axis.ClampedX(tick.TimeMs);
            if (x + 28 > ruler.End.X)
                continue;

            canvas.DrawTextStyledLeft(target, new Rect2(x + 3, ruler.Position.Y + 1, 60, 12), label, color, fontSize);
        }
    }

    /// <summary>A line from <paramref name="top"/> to <paramref name="bottom"/> at each tick; minor ones only when given a colour.</summary>
    public void GridLines(
        TimelineAxis axis,
        float top,
        float bottom,
        IReadOnlyList<TimelineTick> ticks,
        Color major,
        Color? minor = null)
    {
        foreach (TimelineTick tick in ticks)
        {
            if (!axis.Shows(tick.TimeMs))
                continue;

            Color color = tick.Level == TimelineTickLevel.Major ? major : minor ?? default;
            if (tick.Level != TimelineTickLevel.Major && minor is null)
                continue;

            float x = axis.ClampedX(tick.TimeMs);
            canvas.DrawLine(target, new Vector2(x, top), new Vector2(x, bottom), color, 1);
        }
    }

    /// <summary>The playhead: a line from <paramref name="top"/> to <paramref name="bottom"/>, with a tab on top when <paramref name="tab"/>. Nothing when it is out of the window.</summary>
    public void Playhead(TimelineAxis axis, double ms, float top, float bottom, Color color, bool tab = true)
    {
        if (!axis.Shows(ms))
            return;

        float x = axis.ClampedX(ms);
        canvas.DrawLine(target, new Vector2(x, top), new Vector2(x, bottom), color, PlayheadWidth);
        if (tab)
            canvas.DrawRect(target, new Rect2(x - PlayheadTab / 2, top - 4f, PlayheadTab, PlayheadTab), color);
    }

    /// <summary>A key's marker, shaped by how it eases: a square holds, a diamond is linear, a circle is eased.</summary>
    public void Key(Vector2 center, float radius, Color color, EasePreset ease)
    {
        switch (ease)
        {
            case EasePreset.Step:
                canvas.DrawRect(target, new Rect2(center - new Vector2(radius, radius), new Vector2(radius * 2, radius * 2)), color);
                break;
            case EasePreset.Linear:
                canvas.DrawPolygon(target, new[]
                {
                    center + new Vector2(0, -radius), center + new Vector2(radius, 0),
                    center + new Vector2(0, radius), center + new Vector2(-radius, 0),
                }, color);
                break;
            default:
                canvas.DrawCircle(target, center, radius, color);
                break;
        }
    }

    /// <summary>A key marker at a time on a row's middle, the selected one larger; nothing when it is out of the window.</summary>
    public void Key(TimelineAxis axis, Rect2 row, double ms, Color color, EasePreset ease, bool selected)
    {
        if (!axis.Shows(ms))
            return;

        Key(
            new Vector2(axis.X(ms), row.Position.Y + row.Size.Y / 2f),
            selected ? KeyRadius + 1 : KeyRadius - 1,
            selected ? Colors.White : color,
            ease);
    }

    /// <summary>
    /// A timed change across a row as a band whose thickness follows its
    /// easing (see <see cref="TimelineEaseBand"/>), cut at the axis's ends.
    /// Where the value runs backwards it takes <paramref name="reverse"/>.
    /// </summary>
    public void Span(TimelineAxis axis, Rect2 row, double fromMs, double toMs, EasePreset? ease, Color color, Color? reverse = null) =>
        Fill(TimelineEaseBand.Pieces(ease, axis.X(fromMs), axis.X(toMs), axis.Left, axis.Right, row), color, reverse);

    /// <summary>A span whose band follows any curve, such as a cycle that goes out and back.</summary>
    public void Span(TimelineAxis axis, Rect2 row, double fromMs, double toMs, Func<float, float> progress, Color color) =>
        Fill(TimelineEaseBand.Pieces(progress, axis.X(fromMs), axis.X(toMs), axis.Left, axis.Right, row), color, null);

    /// <summary>A flat bar across a row between two times, inset from its top and bottom, cut at the axis's ends.</summary>
    public void Bar(TimelineAxis axis, Rect2 row, double fromMs, double toMs, Color color, float inset = 1f)
    {
        float left = Math.Max(axis.X(fromMs), axis.Left);
        float right = Math.Min(axis.X(toMs), axis.Right);
        if (right > left)
            canvas.DrawRect(target, new Rect2(left, row.Position.Y + inset, right - left, row.Size.Y - inset * 2), color);
    }

    private void Fill(List<EaseBandPiece> pieces, Color color, Color? reverse)
    {
        foreach (EaseBandPiece piece in pieces)
            canvas.DrawPolygon(target, piece.Polygon, piece.Reverse && reverse is { } back ? back : color);
    }
}
