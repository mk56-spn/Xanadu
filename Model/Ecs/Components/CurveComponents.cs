using System;
using Xanadu.Core.Maths;

namespace Xanadu.Model.Ecs.Components;

/// <summary>
/// The path a curve note steers the body along once its head is hit: the
/// points it runs through, on the playfield, each with the body's speed there.
/// Point 0 is the head, where the note sits; the note's time is when the body
/// sets off from it. Speeds ease between points along the length, so the
/// path's end time follows from its length and speeds
/// (<see cref="Xanadu.Model.Playfields.CurvePath"/>).
/// </summary>
/// <remarks>
/// Never changed in place: moving or reshaping makes a new one, so the copies
/// undo, copy and paste keep can share it safely.
/// </remarks>
public sealed class NotePath : IEquatable<NotePath>
{
    /// <summary>A path needs the head and one more point.</summary>
    public const int MinPoints = 2;

    public const int MaxPoints = 256;

    /// <summary>Speeds a point may carry, playfield units a second.</summary>
    public const float MinSpeed = 50f;

    public const float MaxSpeed = 4000f;

    private readonly Vector2[] _points;
    private readonly float[] _speeds;

    public NotePath(ReadOnlySpan<Vector2> points, ReadOnlySpan<float> speeds)
    {
        if (points.Length != speeds.Length)
            throw new ArgumentException("A note path needs one speed per point.");
        _points = points.ToArray();
        _speeds = speeds.ToArray();
    }

    /// <summary>The points the path runs through, in order; the first is the head.</summary>
    public ReadOnlySpan<Vector2> Points => _points;

    /// <summary>The body's speed at each point, playfield units a second.</summary>
    public ReadOnlySpan<float> Speeds => _speeds;

    public int Count => _points.Length;

    /// <summary>The upright box around every point.</summary>
    public Rect2 Bounds
    {
        get
        {
            var bounds = new Rect2(_points.Length > 0 ? _points[0] : Vector2.Zero, Vector2.Zero);
            foreach (Vector2 point in _points)
                bounds = bounds.Expand(point);
            return bounds;
        }
    }

    /// <summary>The same path moved by <paramref name="offset"/>.</summary>
    public NotePath Moved(Vector2 offset)
    {
        var points = (Vector2[])_points.Clone();
        for (int i = 0; i < points.Length; i++)
            points[i] += offset;
        return new NotePath(points, _speeds);
    }

    /// <summary>The same path with one point somewhere else.</summary>
    public NotePath WithPoint(int index, Vector2 point)
    {
        var points = (Vector2[])_points.Clone();
        points[index] = point;
        return new NotePath(points, _speeds);
    }

    public bool Equals(NotePath? other) =>
        other is not null &&
        (ReferenceEquals(this, other) ||
         (_points.AsSpan().SequenceEqual(other._points) && _speeds.AsSpan().SequenceEqual(other._speeds)));

    public override bool Equals(object? obj) => obj is NotePath other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (Vector2 point in _points)
            hash.Add(point);
        foreach (float speed in _speeds)
            hash.Add(speed);
        return hash.ToHashCode();
    }

    /// <summary>Orders paths by point count, then point by point; null first.</summary>
    public static int Compare(NotePath? a, NotePath? b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a is null)
            return -1;
        if (b is null)
            return 1;
        int order = a.Count.CompareTo(b.Count);
        for (int i = 0; order == 0 && i < a.Count; i++)
        {
            order = a._points[i].X.CompareTo(b._points[i].X);
            if (order == 0)
                order = a._points[i].Y.CompareTo(b._points[i].Y);
            if (order == 0)
                order = a._speeds[i].CompareTo(b._speeds[i]);
        }

        return order;
    }
}
