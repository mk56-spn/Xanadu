using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Ui.Editing;

/// <summary>
/// One box on a board: where it sits in board space, its text and its colour (a theme category). A box may also say
/// what it is (<see cref="Kind"/>, right of its text) and what it holds (<see cref="Value"/>, in a box under them);
/// both are shown, not typed into. A <see cref="Flag"/> is a word of warning, a tag on its corner.
/// </summary>
public sealed class BoardBox
{
    public int Id;
    public Rect2 Rect;
    public string Text = "";
    public string Kind = "", Value = "";
    public int Category;
    public string Flag = "";
    public FlagTone FlagTone;

    public bool Labelled => Kind.Length > 0 || Value.Length > 0;

    public BoardBox Clone() => new()
    {
        Id = Id, Rect = Rect, Text = Text, Kind = Kind, Value = Value, Category = Category, Flag = Flag, FlagTone = FlagTone,
    };
}

/// <summary>How serious a box's flag is: worth a look, or wrong.</summary>
public enum FlagTone
{
    Warn,
    Bad,
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

    /// <summary>How far a line keeps off a box it passes, in board units.</summary>
    public const float Clearance = 8;

    /// <summary>Whether there is a line back the other way, so this one points both ways.</summary>
    public bool TwoWay(BoardLink link) => Links.Contains(new BoardLink(link.To, link.From));

    /// <summary>A line whose partner the other way came first: that partner draws them both.</summary>
    internal bool DrawnByPartner(BoardLink link) => Links.IndexOf(new BoardLink(link.To, link.From)) is var other and >= 0 && other < Links.IndexOf(link);

    public readonly List<BoardBox> Boxes = [];
    public readonly List<BoardLink> Links = [];

    /// <summary>The id the next new box takes.</summary>
    public int NextId = 1;

    public BoardBox Add(Rect2 rect, string text = "", int category = 0, string kind = "", string value = "")
    {
        var box = new BoardBox { Id = NextId++, Rect = rect, Text = text, Kind = kind, Value = value, Category = category };
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
        List<List<Vector2>?> paths = Paths();
        for (int at = 0; at < paths.Count; at++)
        {
            if (paths[at] is not { } path)
                continue;

            for (int i = 1; i < path.Count; i++)
            {
                float distance = DistanceToSegment(point, path[i - 1], path[i]);
                if (distance <= bestDistance)
                {
                    best = Links[at];
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    /// <summary>The points a line is drawn through, turning only at right angles (<see cref="BoardRoutes"/>).</summary>
    public List<Vector2>? Path(BoardLink link)
    {
        int index = Links.IndexOf(link);
        return index < 0 ? null : Paths()[index];
    }

    /// <summary>
    /// Every line's <see cref="Path"/>, in the order of <see cref="Links"/> (null where a box is gone, or where the
    /// line back the other way draws both), worked out together so lines keep apart.
    /// </summary>
    public List<List<Vector2>?> Paths() => BoardRoutes.Paths(this);

    /// <summary>Whether the segment passes through the inside of a rect, and how far along (0..1) it gets in.</summary>
    public static bool Crosses(Vector2 a, Vector2 b, Rect2 rect, out float at) => Span(a, b, rect, out at, out _);

    /// <summary>The stretch (0..1 along the segment) spent inside a rect; false when it never gets in.</summary>
    private static bool Span(Vector2 a, Vector2 b, Rect2 rect, out float enter, out float leave)
    {
        enter = 0;
        leave = 1;
        Vector2 d = b - a;
        return Clip(d.X, rect.Position.X - a.X, rect.End.X - a.X, ref enter, ref leave)
            && Clip(d.Y, rect.Position.Y - a.Y, rect.End.Y - a.Y, ref enter, ref leave);
    }

    private static bool Clip(float d, float low, float high, ref float enter, ref float leave)
    {
        if (d == 0)
            return low < 0 && high > 0;

        float first = low / d, second = high / d;
        if (first > second)
            (first, second) = (second, first);
        enter = MathF.Max(enter, first);
        leave = MathF.Min(leave, second);
        return enter < leave;
    }

    /// <summary>Where a line is drawn: from the edge of its first box to the edge of its second, along the line between their centres.</summary>
    public (Vector2 From, Vector2 To)? Ends(BoardLink link) =>
        Find(link.From) is { } from && Find(link.To) is { } to ? Ends(from.Rect, to.Rect) : null;

    /// <summary>The same, with the whole line moved <paramref name="aside"/> to its right-hand side.</summary>
    public static (Vector2 From, Vector2 To) Ends(Rect2 from, Rect2 to, float aside = 0)
    {
        Vector2 a = from.GetCenter(), b = to.GetCenter();
        if (aside != 0 && a != b)
        {
            Vector2 along = Vector2.Normalize(b - a);
            Vector2 start = a + new Vector2(-along.Y, along.X) * aside, d = b - a;
            if (Span(start, start + d, from, out _, out float leave) && Span(start, start + d, to, out float enter, out _) && leave < enter)
                return (start + d * leave, start + d * enter);
        }

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
