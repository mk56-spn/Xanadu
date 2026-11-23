using System.Collections.Generic;

namespace Xanadu.Core.Items;

/// <summary>
/// Edits on a <see cref="Xif"/> that any editor can make: the animator on a
/// rig effect, the level editor on a level item. Each returns whether it changed
/// anything, so a host records an undo step only for real edits. Polygons
/// never drop below <see cref="MinPolygonPoints"/>, since fewer draws nothing.
/// </summary>
public static class XifEditing
{
    public const int MinPolygonPoints = 3;

    /// <summary>The fewest points a curve keeps: two segments, each an on-curve point and its control.</summary>
    public const int MinCurvePoints = 4;

    /// <summary>A new polygon's outline: a small square around the origin.</summary>
    public static List<Vector2> DefaultOutline() =>
    [
        new Vector2(-16, -16),
        new Vector2(16, -16),
        new Vector2(16, 16),
        new Vector2(-16, 16)
    ];

    public static bool Rename(Xif item, string name)
    {
        string trimmed = (name ?? "").Trim();
        if (trimmed == item.Name)
            return false;

        item.Name = trimmed;
        return true;
    }

    /// <summary>Adds a white polygon to the item's geometry, adding the geometry if it has none; returns its index.</summary>
    public static int AddPolygon(Xif item, List<Vector2>? outline = null)
    {
        List<XifPolygon> polygons = item.Need<XifGeometry>().Polygons;
        polygons.Add(new XifPolygon { Points = outline ?? DefaultOutline() });
        return polygons.Count - 1;
    }

    /// <summary>How many points an ellipse's outline has.</summary>
    public const int EllipsePoints = 32;

    /// <summary>Adds a primitive (a quad or an ellipse) 32 units across round the origin; returns its index.</summary>
    public static int AddPrimitive(Xif item, XifShapeKind kind)
    {
        var shape = new XifPolygon { Kind = kind };
        Reshape(shape, new Vector2(-16, -16), new Vector2(16, 16));
        List<XifPolygon> polygons = item.Need<XifGeometry>().Polygons;
        polygons.Add(shape);
        return polygons.Count - 1;
    }

    /// <summary>
    /// The points a shape is dragged by: a free shape's own points; a quad's
    /// four corners; an ellipse's right, bottom, left and top.
    /// </summary>
    public static List<Vector2> Handles(XifPolygon shape)
    {
        if (shape.Kind is XifShapeKind.Free or XifShapeKind.Curve)
            return shape.Points;
        (Vector2 min, Vector2 max) = Box(shape);
        Vector2 centre = (min + max) / 2;
        return shape.Kind == XifShapeKind.Quad
            ? [min, new Vector2(max.X, min.Y), max, new Vector2(min.X, max.Y)]
            : [new Vector2(max.X, centre.Y), new Vector2(centre.X, max.Y), new Vector2(min.X, centre.Y), new Vector2(centre.X, min.Y)];
    }

    /// <summary>
    /// Moves one handle: a free shape's point; a quad's corner, the opposite
    /// corner staying put; an ellipse's side, the opposite side staying put.
    /// </summary>
    public static bool MoveHandle(XifPolygon shape, int handle, Vector2 position)
    {
        if (shape.Kind is XifShapeKind.Free or XifShapeKind.Curve)
            return MovePoint(shape, handle, position);
        if (handle < 0 || handle > 3)
            return false;

        (Vector2 min, Vector2 max) = Box(shape);
        if (shape.Kind == XifShapeKind.Quad)
        {
            Vector2 opposite = Handles(shape)[(handle + 2) % 4];
            min = Vector2.Min(opposite, position);
            max = Vector2.Max(opposite, position);
        }
        else
        {
            switch (handle)
            {
                case 0: max.X = MathF.Max(position.X, min.X + 1); break;
                case 1: max.Y = MathF.Max(position.Y, min.Y + 1); break;
                case 2: min.X = MathF.Min(position.X, max.X - 1); break;
                default: min.Y = MathF.Min(position.Y, max.Y - 1); break;
            }
        }
        if (max.X - min.X < 1 || max.Y - min.Y < 1)
            return false;
        Reshape(shape, min, max);
        return true;
    }

    /// <summary>Rebuilds a primitive's outline to fill the box from <paramref name="min"/> to <paramref name="max"/>.</summary>
    public static void Reshape(XifPolygon shape, Vector2 min, Vector2 max)
    {
        shape.Points.Clear();
        if (shape.Kind == XifShapeKind.Quad)
        {
            shape.Points.AddRange([min, new Vector2(max.X, min.Y), max, new Vector2(min.X, max.Y)]);
            return;
        }
        Vector2 centre = (min + max) / 2, radius = (max - min) / 2;
        if (shape.Kind == XifShapeKind.Curve)
        {
            // Four quarter arcs: on-curve at the sides, each pulled by the box corner between.
            shape.Points.AddRange([
                new Vector2(max.X, centre.Y), max,
                new Vector2(centre.X, max.Y), new Vector2(min.X, max.Y),
                new Vector2(min.X, centre.Y), min,
                new Vector2(centre.X, min.Y), new Vector2(max.X, min.Y),
            ]);
            return;
        }
        for (int i = 0; i < EllipsePoints; i++)
        {
            float angle = MathF.Tau * i / EllipsePoints;
            shape.Points.Add(centre + new Vector2(MathF.Cos(angle) * radius.X, MathF.Sin(angle) * radius.Y));
        }
    }

    /// <summary>The smallest box holding a shape's points.</summary>
    public static (Vector2 Min, Vector2 Max) Box(XifPolygon shape)
    {
        if (shape.Points.Count == 0)
            return (Vector2.Zero, Vector2.Zero);
        Vector2 min = shape.Points[0], max = shape.Points[0];
        foreach (Vector2 point in shape.Points)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return (min, max);
    }

    /// <summary>Turns the shader's fitting to the shape on or off.</summary>
    public static bool SetFit(Xif item, bool fit)
    {
        XifShader shader = item.Need<XifShader>();
        if (shader.Fit == fit)
            return false;
        shader.Fit = fit;
        return true;
    }

    /// <summary>Removes a polygon. The last one stays, so the item always draws something.</summary>
    public static bool RemovePolygon(Xif item, int index)
    {
        List<XifPolygon>? polygons = item.Get<XifGeometry>()?.Polygons;
        if (polygons is null || index < 0 || index >= polygons.Count || polygons.Count <= 1)
            return false;

        polygons.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Inserts a point half way along the edge that leaves <paramref name="after"/>,
    /// so the outline does not change until the new point is moved. Returns
    /// its index, or -1.
    /// </summary>
    public static int InsertPoint(XifPolygon polygon, int after)
    {
        List<Vector2> points = polygon.Points;
        if (polygon.Kind == XifShapeKind.Curve)
            return SplitSegment(points, after);
        // A primitive keeps its outline; only a free shape takes new points.
        if (points.Count == 0 || polygon.Kind != XifShapeKind.Free)
            return -1;

        int from = Math.Clamp(after, 0, points.Count - 1);
        Vector2 middle = (points[from] + points[(from + 1) % points.Count]) / 2;
        points.Insert(from + 1, middle);
        return from + 1;
    }

    /// <summary>
    /// Cuts the curve segment holding point <paramref name="after"/> in two at
    /// its middle, exactly (de Casteljau on its three points), so the outline
    /// does not change; returns the new on-curve point's index, or -1.
    /// </summary>
    private static int SplitSegment(List<Vector2> points, int after)
    {
        if (points.Count < MinCurvePoints || points.Count % 2 != 0)
            return -1;
        int start = Math.Clamp(after, 0, points.Count - 1) & ~1;
        Vector2 from = points[start], control = points[start + 1], to = points[(start + 2) % points.Count];
        Vector2 left = (from + control) / 2, right = (control + to) / 2, middle = (left + right) / 2;
        points[start + 1] = left;
        points.InsertRange(start + 2, [middle, right]);
        return start + 2;
    }

    /// <summary>
    /// Takes out the curve's on-curve point at or before <paramref name="index"/>,
    /// joining its two segments into one pulled by their controls' midpoint.
    /// </summary>
    private static bool JoinSegments(List<Vector2> points, int index)
    {
        if (points.Count <= MinCurvePoints || points.Count % 2 != 0 || index < 0 || index >= points.Count)
            return false;
        int on = index & ~1;
        int before = (on - 1 + points.Count) % points.Count;
        points[before] = (points[before] + points[on + 1]) / 2;
        points.RemoveRange(on, 2);
        return true;
    }

    public static bool RemovePoint(XifPolygon polygon, int index)
    {
        if (polygon.Kind == XifShapeKind.Curve)
            return JoinSegments(polygon.Points, index);
        if (polygon.Kind != XifShapeKind.Free || index < 0 || index >= polygon.Points.Count || polygon.Points.Count <= MinPolygonPoints)
            return false;

        polygon.Points.RemoveAt(index);
        return true;
    }

    public static bool MovePoint(XifPolygon polygon, int index, Vector2 position)
    {
        if (index < 0 || index >= polygon.Points.Count ||
            !float.IsFinite(position.X) || !float.IsFinite(position.Y) ||
            polygon.Points[index] == position)
        {
            return false;
        }

        polygon.Points[index] = position;
        return true;
    }

    public static bool SetFill(XifPolygon polygon, Color fill)
    {
        if (polygon.Fill == fill)
            return false;

        polygon.Fill = fill;
        return true;
    }

    /// <summary>Reads a fill typed as html hex (<c>#rrggbb</c> or <c>#rrggbbaa</c>).</summary>
    public static bool TryParseFill(string text, out Color fill)
    {
        string trimmed = (text ?? "").Trim();
        fill = Colors.White;
        if (trimmed.Length == 0 || !Color.HtmlIsValid(trimmed))
            return false;

        fill = Color.FromHtml(trimmed);
        return true;
    }

    public static string FormatFill(Color fill) => "#" + fill.ToHtml(includeAlpha: true);

    /// <summary>Points the item's shader module at a shader, adding the module if it has none.</summary>
    public static bool SetShader(Xif item, XifShaderRef shader)
    {
        if (item.ShaderRef.Same(shader))
            return false;

        item.Need<XifShader>().Shader = shader.Copy();
        return true;
    }

    /// <summary>Stacks a common shader on top of the item's others; false when it is already there.</summary>
    public static bool AddLayer(Xif item, string path)
    {
        XifShader shader = item.Need<XifShader>();
        if (path.Length == 0 || shader.Layers.Contains(path) || shader.Shader.Path == path)
            return false;
        shader.Layers.Add(path);
        return true;
    }

    /// <summary>Takes a layer off the item's stack.</summary>
    public static bool RemoveLayer(Xif item, int index)
    {
        List<string>? layers = item.Get<XifShader>()?.Layers;
        if (layers is null || index < 0 || index >= layers.Count)
            return false;
        layers.RemoveAt(index);
        return true;
    }

    /// <summary>Moves a layer one step up (+1) or down (-1) the stack.</summary>
    public static bool MoveLayer(Xif item, int index, int step)
    {
        List<string>? layers = item.Get<XifShader>()?.Layers;
        int to = index + step;
        if (layers is null || index < 0 || index >= layers.Count || to < 0 || to >= layers.Count)
            return false;
        (layers[index], layers[to]) = (layers[to], layers[index]);
        return true;
    }

    /// <summary>Sets one of the shader's tuned values (one float per channel); false when it already had them.</summary>
    public static bool SetParam(Xif item, string name, float[] values)
    {
        XifShader shader = item.Need<XifShader>();
        if (shader.Params.TryGetValue(name, out float[]? current) && current.AsSpan().SequenceEqual(values))
            return false;
        shader.Params[name] = (float[])values.Clone();
        return true;
    }

    /// <summary>The item-space box holding every polygon point and the origin, never empty.</summary>
    public static Rect2 Bounds(Xif item)
    {
        var bounds = new Rect2(Vector2.Zero, Vector2.Zero);
        foreach (XifPolygon polygon in item.Polygons)
        {
            foreach (Vector2 point in polygon.Points)
                bounds = bounds.Expand(point);
        }
        return bounds.Size.X < 1f || bounds.Size.Y < 1f ? bounds.Grow(8f) : bounds;
    }
}
