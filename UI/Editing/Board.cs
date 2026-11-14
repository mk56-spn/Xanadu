using System;
using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Ui.Editing;

/// <summary>One box on a board: where it sits in board space, its text and its colour (a theme category).</summary>
public sealed class BoardBox
{
    public int Id;
    public Rect2 Rect;
    public string Text = "";
    public int Category;

    public BoardBox Clone() => new() { Id = Id, Rect = Rect, Text = Text, Category = Category };
}

/// <summary>A line from one box to another, drawn with an arrowhead at <see cref="To"/>.</summary>
public readonly record struct BoardLink(int From, int To);

/// <summary>
/// A whiteboard of boxes and the lines between them, in board space (zoom and
/// pan are the view's, not the board's). A box that wholly holds other boxes
/// is a frame: it draws behind them with its text as a title, and moving it
/// carries them. Nothing here knows what the boxes mean.
/// </summary>
public sealed class Board
{
    /// <summary>Boxes snap to this many board units.</summary>
    public const float Grid = 10;

    public const float MinWidth = 40, MinHeight = 30;

    public readonly List<BoardBox> Boxes = new();
    public readonly List<BoardLink> Links = new();

    /// <summary>The id the next new box takes.</summary>
    public int NextId = 1;

    public BoardBox Add(Rect2 rect, string text = "", int category = 0)
    {
        var box = new BoardBox { Id = NextId++, Rect = rect, Text = text, Category = category };
        Boxes.Add(box);
        return box;
    }

    public BoardBox? Find(int id) => Boxes.Find(box => box.Id == id);

    /// <summary>Takes a box off the board, with every line to or from it.</summary>
    public void Remove(int id)
    {
        Boxes.RemoveAll(box => box.Id == id);
        Links.RemoveAll(link => link.From == id || link.To == id);
    }

    /// <summary>Joins two boxes; false for a box to itself, a missing box or a line already there.</summary>
    public bool Link(int from, int to)
    {
        var link = new BoardLink(from, to);
        if (from == to || Find(from) is null || Find(to) is null || Links.Contains(link))
            return false;

        Links.Add(link);
        return true;
    }

    /// <summary>The smallest box under a point, so a box wins over the frame around it; null over empty board.</summary>
    public BoardBox? At(Vector2 point)
    {
        BoardBox? best = null;
        foreach (BoardBox box in Boxes)
        {
            if (box.Rect.HasPoint(point) && (best is null || Area(box) < Area(best)))
                best = box;
        }

        return best;
    }

    /// <summary>The boxes wholly inside <paramref name="frame"/> and smaller than it: what it carries.</summary>
    public IEnumerable<BoardBox> Inside(BoardBox frame) =>
        Boxes.Where(box => box != frame && Area(box) < Area(frame) && frame.Rect.Encloses(box.Rect));

    public bool IsFrame(BoardBox box) => Inside(box).Any();

    /// <summary>Biggest first, so frames sit behind what they hold; ties keep the order boxes were made in.</summary>
    public IEnumerable<BoardBox> PaintOrder() => Boxes.OrderByDescending(Area);

    /// <summary>The line passing within <paramref name="reach"/> of a point, nearest first.</summary>
    public BoardLink? LinkAt(Vector2 point, float reach)
    {
        BoardLink? best = null;
        float bestDistance = reach;
        foreach (BoardLink link in Links)
        {
            if (Ends(link) is not { } ends)
                continue;

            float distance = DistanceToSegment(point, ends.From, ends.To);
            if (distance <= bestDistance)
            {
                best = link;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Where a line is drawn: from the edge of its first box to the edge of its second, along the line between their centres.</summary>
    public (Vector2 From, Vector2 To)? Ends(BoardLink link) =>
        Find(link.From) is { } from && Find(link.To) is { } to ? Ends(from.Rect, to.Rect) : null;

    public static (Vector2 From, Vector2 To) Ends(Rect2 from, Rect2 to)
    {
        Vector2 a = from.GetCenter(), b = to.GetCenter();
        return (Edge(from, b), Edge(to, a));
    }

    /// <summary>Where the line from a box's centre towards <paramref name="toward"/> leaves the box.</summary>
    public static Vector2 Edge(Rect2 rect, Vector2 toward)
    {
        Vector2 centre = rect.GetCenter();
        Vector2 d = toward - centre;
        if (d == Vector2.Zero)
            return centre;

        float half = MathF.Min(
            d.X == 0 ? float.MaxValue : rect.Size.X / 2 / MathF.Abs(d.X),
            d.Y == 0 ? float.MaxValue : rect.Size.Y / 2 / MathF.Abs(d.Y));
        return centre + d * MathF.Min(half, 1);
    }

    public static float Snap(float value) => MathF.Round(value / Grid) * Grid;

    public static Vector2 Snap(Vector2 value) => new(Snap(value.X), Snap(value.Y));

    public static float Area(BoardBox box) => box.Rect.Size.X * box.Rect.Size.Y;

    public static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float length = ab.LengthSquared();
        float t = length == 0 ? 0 : Math.Clamp(Vector2.Dot(point - a, ab) / length, 0, 1);
        return Vector2.Distance(point, a + ab * t);
    }

    /// <summary>A copy that shares nothing with this one, for undo.</summary>
    public Board Clone()
    {
        var copy = new Board { NextId = NextId };
        copy.Boxes.AddRange(Boxes.Select(box => box.Clone()));
        copy.Links.AddRange(Links);
        return copy;
    }

    /// <summary>Makes this board what <paramref name="other"/> is, keeping this object (owners hold it).</summary>
    public void CopyFrom(Board other)
    {
        Boxes.Clear();
        Boxes.AddRange(other.Boxes.Select(box => box.Clone()));
        Links.Clear();
        Links.AddRange(other.Links);
        NextId = other.NextId;
    }
}
