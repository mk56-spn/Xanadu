using System;
using System.Collections.Generic;
using System.Linq;
using Xanadu.Core.Items;
using Xanadu.Core.Maths;
using Xanadu.Core.Rendering;

namespace Xanadu.Model.Animation;

/// <summary>
/// A geometry's polygons triangulated into one mesh, each vertex coloured by
/// its polygon's fill. A curve shape adds one triangle per bent segment, its
/// corners in order start, control, end, tagged in <see cref="Curves"/> so the
/// GPU trims it to the curve; the CPU never samples a curve.
/// </summary>
public sealed record ArtMesh(Vector2[] Points, Color[] Colors, int[] Triangles, Rect2 Bounds)
{
    /// <summary>A triangle the GPU fills between the chord and the curve: the curve bulges out of the shape.</summary>
    public const byte ConvexCurve = 1;

    /// <summary>A triangle the GPU fills between the curve and its control: the curve bends into the shape.</summary>
    public const byte ConcaveCurve = 2;

    /// <summary>Per triangle: 0 plain, else <see cref="ConvexCurve"/> or <see cref="ConcaveCurve"/>. Empty when the mesh has no curves.</summary>
    public byte[] Curves { get; init; } = [];

    /// <summary>The curve tag of the triangle holding triangle index <paramref name="index"/> (a position in <see cref="Triangles"/>).</summary>
    public byte CurveAt(int index) => Curves.Length == 0 ? (byte)0 : Curves[index / 3];

    public static ArtMesh Of(XifGeometry? geometry) => Build(geometry, 0f);

    /// <summary>
    /// The geometry with its edges cut so no piece is longer than a share of
    /// its size, for art laid along a bending strip.
    /// </summary>
    public static ArtMesh Dense(XifGeometry? geometry, int pieces)
    {
        Rect2 bounds = BoundsOf(geometry);
        float spacing = Math.Max(bounds.Size.X, bounds.Size.Y) / Math.Max(4, pieces);
        return Build(geometry, spacing);
    }

    private static ArtMesh Build(XifGeometry? geometry, float spacing)
    {
        var points = new List<Vector2>();
        var colors = new List<Color>();
        var triangles = new List<int>();
        var curves = new List<byte>();
        if (geometry is not null)
        {
            foreach (XifPolygon polygon in geometry.Polygons)
            {
                if (polygon.Kind == XifShapeKind.Curve)
                {
                    AddCurve(polygon, points, colors, triangles, curves);
                    continue;
                }
                if (polygon.Points.Count < 3)
                    continue;
                Vector2[] outline = spacing > 0f
                    ? RigSkinBuilder.Resample(polygon.Points.ToArray(), spacing).ToArray()
                    : polygon.Points.ToArray();
                int[] local = Triangulate.Polygon(outline);
                if (local.Length == 0)
                    continue;
                int start = points.Count;
                points.AddRange(outline);
                for (int i = 0; i < outline.Length; i++)
                    colors.Add(polygon.Fill);
                foreach (int index in local)
                    triangles.Add(start + index);
                for (int i = 0; i < local.Length / 3; i++)
                    curves.Add(0);
            }
        }
        bool curved = curves.Exists(static tag => tag != 0);
        return new ArtMesh(points.ToArray(), colors.ToArray(), triangles.ToArray(), BoundsOf(geometry))
        {
            Curves = curved ? curves.ToArray() : [],
        };
    }

    /// <summary>
    /// A curve shape: its straight hull (each on-curve point, plus the control
    /// of every segment that bends inward) triangulated as usual, then one
    /// triangle per bent segment for the GPU to trim to the curve. Overlapping
    /// segments are not resolved.
    /// </summary>
    private static void AddCurve(XifPolygon polygon, List<Vector2> points, List<Color> colors, List<int> triangles, List<byte> curves)
    {
        List<Vector2> cage = polygon.Points;
        if (cage.Count < 4 || cage.Count % 2 != 0)
            return;

        // Which way the on-curve points wind: the inside is on that side of each chord.
        float area = 0f;
        for (int i = 0; i < cage.Count; i += 2)
            area += Cross(cage[i], cage[(i + 2) % cage.Count]);
        if (MathF.Abs(area) < 1e-6f)
            return;

        var hull = new List<Vector2>();
        var bent = new List<(Vector2 From, Vector2 Control, Vector2 To, byte Tag)>();
        for (int i = 0; i < cage.Count; i += 2)
        {
            Vector2 from = cage[i], control = cage[i + 1], to = cage[(i + 2) % cage.Count];
            hull.Add(from);
            float side = Cross(to - from, control - from);
            float chord = (to - from).Length();
            if (MathF.Abs(side) <= 1e-4f * MathF.Max(1f, chord * chord))
                continue;
            bool inward = side * area > 0f;
            if (inward)
                hull.Add(control);
            bent.Add((from, control, to, inward ? ConcaveCurve : ConvexCurve));
        }

        int[] local = Triangulate.Polygon(hull.ToArray());
        int start = points.Count;
        points.AddRange(hull);
        for (int i = 0; i < hull.Count; i++)
            colors.Add(polygon.Fill);
        foreach (int index in local)
            triangles.Add(start + index);
        for (int i = 0; i < local.Length / 3; i++)
            curves.Add(0);

        foreach ((Vector2 from, Vector2 control, Vector2 to, byte tag) in bent)
        {
            int first = points.Count;
            points.AddRange([from, control, to]);
            colors.AddRange([polygon.Fill, polygon.Fill, polygon.Fill]);
            triangles.AddRange([first, first + 1, first + 2]);
            curves.Add(tag);
        }
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    /// <summary>The same mesh moved so its bounds are centred on the origin.</summary>
    public ArtMesh Centred()
    {
        Vector2 centre = Bounds.GetCenter();
        var moved = new Vector2[Points.Length];
        for (int i = 0; i < moved.Length; i++)
            moved[i] = Points[i] - centre;
        return this with { Points = moved, Bounds = new Rect2(Bounds.Position - centre, Bounds.Size) };
    }

    public static Rect2 BoundsOf(XifGeometry? geometry)
    {
        Rect2? bounds = null;
        if (geometry is not null)
        {
            foreach (XifPolygon polygon in geometry.Polygons)
            {
                foreach (Vector2 point in polygon.Points)
                    bounds = bounds is { } current ? current.Expand(point) : new Rect2(point, Vector2.Zero);
            }
        }
        return bounds ?? new Rect2();
    }

    /// <summary>The fill of the topmost polygon holding <paramref name="point"/>, or of the nearest one's first point.</summary>
    public static Color ColorAt(XifGeometry geometry, Vector2 point)
    {
        for (int i = geometry.Polygons.Count - 1; i >= 0; i--)
        {
            XifPolygon polygon = geometry.Polygons[i];
            if (polygon.Points.Count >= 3 && Holds(polygon.Points, point))
                return polygon.Fill;
        }

        Color nearest = Xanadu.Core.Maths.Colors.White;
        float best = float.MaxValue;
        foreach (XifPolygon polygon in geometry.Polygons)
        {
            if (polygon.Points.Count == 0)
                continue;
            float distance = (polygon.Points[0] - point).LengthSquared();
            if (distance < best)
            {
                best = distance;
                nearest = polygon.Fill;
            }
        }
        return nearest;
    }

    /// <summary>Even-odd: whether a ray from the point crosses the outline an odd number of times.</summary>
    private static bool Holds(List<Vector2> outline, Vector2 point)
    {
        bool inside = false;
        for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
        {
            Vector2 a = outline[i], b = outline[j];
            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }
}
