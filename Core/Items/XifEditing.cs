using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Items;

/// <summary>
/// Edits on a <see cref="Xif"/> that any editor can make: the animator on a
/// rig effect, the level editor on a note. Each returns whether it changed
/// anything, so a host records an undo step only for real edits. Polygons
/// never drop below <see cref="MinPolygonPoints"/>, since fewer draws nothing.
/// </summary>
public static class XifEditing
{
    public const int MinPolygonPoints = 3;

    /// <summary>A new polygon's outline: a small square around the origin.</summary>
    public static List<Vector2> DefaultOutline() => new()
    {
        new Vector2(-16, -16),
        new Vector2(16, -16),
        new Vector2(16, 16),
        new Vector2(-16, 16),
    };

    public static bool Rename(Xif item, string name)
    {
        string trimmed = (name ?? "").Trim();
        if (trimmed == item.Name)
            return false;

        item.Name = trimmed;
        return true;
    }

    /// <summary>Adds a white polygon; returns its index.</summary>
    public static int AddPolygon(Xif item, List<Vector2>? outline = null)
    {
        item.Primitives.Add(new XifPrimitive
        {
            Kind = XifPrimitive.PolygonKind,
            Points = outline ?? DefaultOutline(),
        });
        return item.Primitives.Count - 1;
    }

    /// <summary>Removes a primitive. The last one stays, so the item always draws something.</summary>
    public static bool RemovePrimitive(Xif item, int index)
    {
        if (index < 0 || index >= item.Primitives.Count || item.Primitives.Count <= 1)
            return false;

        item.Primitives.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Inserts a point half way along the edge that leaves <paramref name="after"/>,
    /// so the outline does not change until the new point is moved. Returns
    /// its index, or -1.
    /// </summary>
    public static int InsertPoint(XifPrimitive primitive, int after)
    {
        List<Vector2> points = primitive.Points;
        if (!primitive.IsPolygon || points.Count == 0)
            return -1;

        int from = Math.Clamp(after, 0, points.Count - 1);
        Vector2 middle = (points[from] + points[(from + 1) % points.Count]) / 2;
        points.Insert(from + 1, middle);
        return from + 1;
    }

    public static bool RemovePoint(XifPrimitive primitive, int index)
    {
        if (index < 0 || index >= primitive.Points.Count || primitive.Points.Count <= MinPolygonPoints)
            return false;

        primitive.Points.RemoveAt(index);
        return true;
    }

    public static bool MovePoint(XifPrimitive primitive, int index, Vector2 position)
    {
        if (index < 0 || index >= primitive.Points.Count ||
            !float.IsFinite(position.X) || !float.IsFinite(position.Y) ||
            primitive.Points[index] == position)
        {
            return false;
        }

        primitive.Points[index] = position;
        return true;
    }

    public static bool SetFill(XifPrimitive primitive, Color fill)
    {
        if (primitive.Fill == fill)
            return false;

        primitive.Fill = fill;
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

    /// <summary>Points the primitive at a texture, or none for an empty path.</summary>
    public static bool SetTexture(XifPrimitive primitive, string path)
    {
        string trimmed = (path ?? "").Trim();
        if (trimmed == primitive.Ref)
            return false;

        primitive.Ref = trimmed;
        return true;
    }

    public static bool SetShader(XifPrimitive primitive, XifShaderRef shader)
    {
        XifShaderRef current = primitive.Shader;
        if (current.Scope == shader.Scope && current.Slot == shader.Slot && current.Path == shader.Path)
            return false;

        primitive.Shader = new XifShaderRef { Scope = shader.Scope, Slot = shader.Slot, Path = shader.Path };
        return true;
    }

    /// <summary>The item-space box holding every polygon point and the origin, never empty.</summary>
    public static Rect2 Bounds(Xif item)
    {
        var bounds = new Rect2(Vector2.Zero, Vector2.Zero);
        foreach (XifPrimitive primitive in item.Primitives)
        {
            foreach (Vector2 point in primitive.Points)
                bounds = bounds.Expand(point);
        }
        return bounds.Size.X < 1f || bounds.Size.Y < 1f ? bounds.Grow(8f) : bounds;
    }
}
