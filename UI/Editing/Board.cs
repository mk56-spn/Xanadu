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

    /// <summary>How far a line stays off a box it goes round, in board units.</summary>
    public const float Clearance = 8;

    /// <summary>Whether there is a line back the other way, so this one points both ways.</summary>
    public bool TwoWay(BoardLink link) => Links.Contains(new BoardLink(link.To, link.From));

    /// <summary>A line whose partner the other way came first: that partner draws them both.</summary>
    private bool DrawnByPartner(BoardLink link) => Links.IndexOf(new BoardLink(link.To, link.From)) is var other and >= 0 && other < Links.IndexOf(link);

    /// <summary>How much further off a box each further line round it stays.</summary>
    public const float LaneGap = 6;


    private const int MaxDetours = 16, MaxLanes = 6;

    public readonly List<BoardBox> Boxes = [];
    public readonly List<BoardLink> Links = [];

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

    /// <summary>
    /// The points a line is drawn through: straight from box to box when nothing is
    /// in the way, otherwise bent round the corners of the boxes it would cross,
    /// keeping <see cref="Clearance"/> off them. Frames are not in the way (a line
    /// runs over them). Where no way round is found the line stays straight.
    /// </summary>
    public List<Vector2>? Path(BoardLink link)
    {
        int index = Links.IndexOf(link);
        return index < 0 ? null : Paths()[index];
    }

    /// <summary>
    /// Every line's <see cref="Path"/>, in the order of <see cref="Links"/> (null where
    /// a box is gone), worked out together so lines do not lie on each other: each
    /// further line round the same box keeps <see cref="LaneGap"/> more off it, and
    /// two lines joining the same boxes in opposite directions run side by side.
    /// </summary>
    public List<List<Vector2>?> Paths()
    {
        List<BoardBox> solid = [.. Boxes.Where(box => !IsFrame(box))];
        var lanes = new Dictionary<int, int>();
        var paths = new List<List<Vector2>?>(Links.Count);
        var blocks = new List<Block>();
        var rounded = new List<int>();
        foreach (BoardLink link in Links)
        {
            if (Find(link.From) is not { } from || Find(link.To) is not { } to)
            {
                paths.Add(null);
                continue;
            }

            // Lines both ways between two boxes are one line, with an arrow at each end: the first one drawn carries it.
            if (DrawnByPartner(link))
            {
                paths.Add(null);
                continue;
            }

            (Vector2 a, Vector2 b) = Ends(from.Rect, to.Rect);
            blocks.Clear();
            foreach (BoardBox box in solid)
            {
                float clearance = Clearance + LaneGap * (lanes.GetValueOrDefault(box.Id) % MaxLanes);
                Rect2 kept = box.Rect.Grow(clearance);
                if (box != from && box != to && !kept.HasPoint(a) && !kept.HasPoint(b))
                    blocks.Add(new Block(box.Id, box.Rect, clearance));
            }

            var path = new List<Vector2> { a };
            rounded.Clear();
            int detours = MaxDetours;
            if (!Route(a, b, blocks, path, rounded, ref detours))
            {
                paths.Add([a, b]);
                continue;
            }

            foreach (int id in rounded)
                lanes[id] = lanes.GetValueOrDefault(id) + 1;

            if (path.Count > 2)
            {
                path[0] = Edge(from.Rect, path[1]);
                path[^1] = Edge(to.Rect, path[^2]);
            }

            paths.Add(path);
        }

        return paths;
    }

    /// <summary>A box a line must go round, and how far off it this line keeps.</summary>
    private readonly record struct Block(int Id, Rect2 Rect, float Clearance);

    /// <summary>
    /// Adds the way from <paramref name="a"/> (already on the path) to <paramref name="b"/>:
    /// round the shorter side of the first box in the way, then the same for each
    /// piece, noting the boxes gone round. False when the detours run out.
    /// </summary>
    private static bool Route(Vector2 a, Vector2 b, List<Block> blocks, List<Vector2> path, List<int> rounded, ref int detours)
    {
        Block? first = null;
        float firstAt = float.MaxValue;
        foreach (Block block in blocks)
        {
            if (Crosses(a, b, block.Rect.Grow(block.Clearance - 1), out float at) && at < firstAt)
            {
                first = block;
                firstAt = at;
            }
        }

        if (first is not { } hit)
        {
            path.Add(b);
            return true;
        }

        if (detours-- <= 0)
            return false;

        if (!rounded.Contains(hit.Id))
            rounded.Add(hit.Id);

        Rect2 around = hit.Rect.Grow(hit.Clearance);
        Vector2 along = b - a;
        Span<Vector2> corners =
        [
            around.Position, new(around.End.X, around.Position.Y), around.End, new(around.Position.X, around.End.Y),
        ];
        Span<Vector2> left = stackalloc Vector2[4], right = stackalloc Vector2[4];
        int lefts = 0, rights = 0;
        foreach (Vector2 corner in corners)
        {
            if (along.X * (corner.Y - a.Y) - along.Y * (corner.X - a.X) > 0)
                left[lefts++] = corner;
            else
                right[rights++] = corner;
        }

        if (lefts == 0 || rights == 0)
            return false;

        left = left[..lefts];
        right = right[..rights];
        SortAlong(left, along);
        SortAlong(right, along);

        Vector2 from = a;
        foreach (Vector2 corner in Length(a, left, b) <= Length(a, right, b) ? left : right)
        {
            if (!Route(from, corner, blocks, path, rounded, ref detours))
                return false;

            from = corner;
        }

        return Route(from, b, blocks, path, rounded, ref detours);
    }

    private static void SortAlong(Span<Vector2> points, Vector2 along)
    {
        for (int i = 1; i < points.Length; i++)
        {
            for (int j = i; j > 0 && Vector2.Dot(points[j], along) < Vector2.Dot(points[j - 1], along); j--)
                (points[j], points[j - 1]) = (points[j - 1], points[j]);
        }
    }

    private static float Length(Vector2 a, Span<Vector2> through, Vector2 b)
    {
        float length = 0;
        Vector2 from = a;
        foreach (Vector2 point in through)
        {
            length += Vector2.Distance(from, point);
            from = point;
        }

        return length + Vector2.Distance(from, b);
    }

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
