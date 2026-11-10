using System.Collections.Generic;
using Xanadu.Layering;

namespace Xanadu.Animation;

/// <summary>Where each piece of a chain's art goes: one stretched copy per segment.</summary>
public static class XafChainArt
{
    /// <summary>
    /// How much longer than its segment each piece is. Pieces longer than the
    /// spacing overlap, so a chain of rhombi reads as one strand, not as beads.
    /// </summary>
    public const float Overlap = 1.5f;

    /// <summary>Share of the chain's width left at the tip; the strand narrows towards it.</summary>
    public const float TipWidth = 0.45f;

    /// <summary>
    /// The draw layer of every chain's art: one above the highest layer of the
    /// package's attachments, so chains always draw over the body whatever the
    /// order the canvas items happened to be made in.
    /// </summary>
    public static int Layer(XafPackage package)
    {
        int highest = 0;
        foreach (XafAttachment attachment in package.Attachments)
            highest = Mathf.Max(highest, attachment.ZOrder);

        return DrawOrder.ClampLayer(highest + 1);
    }

    /// <summary>A chain's own layer if it has one, else just above the body.</summary>
    public static int LayerOf(XafPackage package, XafChain chain) =>
        chain.Layer is { } layer ? DrawOrder.ClampLayer(layer) : Layer(package);

    /// <summary>The width of the piece on one segment, tapering from the root to the tip.</summary>
    public static float WidthAt(float width, int segment, int segments) =>
        width * Mathf.Lerp(1f, TipWidth, (segment + 0.5f) / Mathf.Max(1, segments));

    /// <summary>
    /// The transform that puts art centred on its own origin onto the segment
    /// from <paramref name="from"/> to <paramref name="to"/>, long side along the
    /// segment and stretched to the piece's length and width. The art is
    /// <paramref name="artSize"/> pixels with its long side down its Y axis.
    /// </summary>
    public static Transform2D Segment(
        Vector2 from,
        Vector2 to,
        int segment,
        int segments,
        float width,
        Vector2 artSize)
    {
        Vector2 along = to - from;
        float length = along.Length();
        Vector2 direction = length > 0.0001f ? along / length : Vector2.Down;
        float lengthScale = length * Overlap / Mathf.Max(1f, artSize.Y);
        float widthScale = WidthAt(width, segment, segments) / Mathf.Max(1f, artSize.X);

        // X is the perpendicular that keeps the transform a proper rotation, not a mirror.
        var across = new Vector2(direction.Y, -direction.X);
        return new Transform2D(across * widthScale, direction * lengthScale, (from + to) / 2f);
    }

    /// <summary>Points the curve takes between two of the chain's, so a sheet bends smoothly rather than at its links.</summary>
    public const int SheetSteps = 4;

    /// <summary>
    /// A sheet along a chain: a smooth curve through its points (Catmull-Rom),
    /// two vertices across it at each step, <paramref name="width"/> apart, and
    /// the triangles between. Texture spots run across (0 to 1) and down from
    /// root to tip by distance along the curve, so the art is laid along it
    /// evenly however the links stretch.
    /// </summary>
    public static (Vector2[] Vertices, Vector2[] Uvs, int[] Triangles) Sheet(IReadOnlyList<Vector2> points, float width)
    {
        if (points.Count < 2)
            return (System.Array.Empty<Vector2>(), System.Array.Empty<Vector2>(), System.Array.Empty<int>());

        var curve = new List<Vector2>();
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 p0 = points[System.Math.Max(0, i - 1)], p1 = points[i], p2 = points[i + 1];
            Vector2 p3 = points[System.Math.Min(points.Count - 1, i + 2)];
            for (int step = 0; step < SheetSteps; step++)
            {
                float t = step / (float)SheetSteps;
                float t2 = t * t, t3 = t2 * t;
                curve.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                                  (3f * p1 - p0 - 3f * p2 + p3) * t3));
            }
        }
        curve.Add(points[^1]);

        var along = new float[curve.Count];
        for (int i = 1; i < curve.Count; i++)
            along[i] = along[i - 1] + curve[i].DistanceTo(curve[i - 1]);
        float total = Mathf.Max(along[^1], 0.0001f);

        var vertices = new Vector2[curve.Count * 2];
        var uvs = new Vector2[curve.Count * 2];
        for (int i = 0; i < curve.Count; i++)
        {
            Vector2 tangent = curve[System.Math.Min(i + 1, curve.Count - 1)] - curve[System.Math.Max(i - 1, 0)];
            tangent = tangent.LengthSquared() > 1e-8f ? tangent.Normalized() : Vector2.Down;
            // Across, the same way the pieces' X runs: down a strand hanging straight, its left is +X.
            var across = new Vector2(tangent.Y, -tangent.X) * (width / 2f);
            vertices[2 * i] = curve[i] - across;
            vertices[2 * i + 1] = curve[i] + across;
            float v = along[i] / total;
            uvs[2 * i] = new Vector2(0f, v);
            uvs[2 * i + 1] = new Vector2(1f, v);
        }

        var triangles = new int[(curve.Count - 1) * 6];
        for (int i = 0; i < curve.Count - 1; i++)
        {
            int a = 2 * i, b = a + 1, c = a + 2, d = a + 3;
            triangles[6 * i] = a;
            triangles[6 * i + 1] = b;
            triangles[6 * i + 2] = d;
            triangles[6 * i + 3] = a;
            triangles[6 * i + 4] = d;
            triangles[6 * i + 5] = c;
        }
        return (vertices, uvs, triangles);
    }
}
