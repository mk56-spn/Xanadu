using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Xanadu.Animation;

/// <summary>
/// The outline of a group of an SVG as closed polygons, in art space (the SVG's
/// units from the top left of its view box). Paths are read with their curves
/// flattened; transforms are not applied, so a skin's art keeps its shapes in
/// the view box's own coordinates.
/// </summary>
public static class XafSvgOutline
{
    /// <summary>How far apart, in SVG units, the points along a flattened curve are.</summary>
    private const float CurveStep = 0.5f;

    /// <summary>The view box of an SVG: where its art starts and how big it is.</summary>
    public static Rect2 ViewBox(string svg)
    {
        XElement root = XDocument.Parse(svg).Root ?? throw new XafFormatException("SVG has no root element.");
        string? box = (string?)root.Attribute("viewBox");
        if (box is null)
        {
            float width = Number((string?)root.Attribute("width") ?? "0");
            float height = Number((string?)root.Attribute("height") ?? "0");
            return new Rect2(0, 0, width, height);
        }
        float[] v = box.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(Number).ToArray();
        return new Rect2(v[0], v[1], v[2], v[3]);
    }

    /// <summary>Every closed shape the group's paths draw, in art space.</summary>
    public static List<Vector2[]> Polygons(string svg, string group)
    {
        Rect2 box = ViewBox(svg);
        XElement root = XDocument.Parse(XafSvgDocument.IsolateGroup(svg, group)).Root!;
        var polygons = new List<Vector2[]>();
        foreach (XElement path in root.Descendants().Where(element => element.Name.LocalName == "path"))
        {
            foreach (List<Vector2> shape in Parse((string?)path.Attribute("d") ?? ""))
            {
                if (shape.Count >= 3)
                    polygons.Add(shape.Select(point => point - box.Position).ToArray());
            }
        }
        return polygons;
    }

    /// <summary>Whether a point lies inside any of the polygons (even-odd within each).</summary>
    public static bool Inside(IReadOnlyList<Vector2[]> polygons, Vector2 point)
    {
        foreach (Vector2[] polygon in polygons)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.Y > point.Y) != (b.Y > point.Y) &&
                    point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                {
                    inside = !inside;
                }
            }
            if (inside)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Reads path data into flattened sub-paths: moves, lines (and their
    /// horizontal and vertical forms), cubic and quadratic curves with their
    /// smooth forms, and closes, absolute and relative. Arcs are taken as
    /// straight lines to their end.
    /// </summary>
    public static List<List<Vector2>> Parse(string data)
    {
        var shapes = new List<List<Vector2>>();
        List<Vector2>? shape = null;
        var tokens = Tokens(data);
        int at = 0;
        char command = 'M';
        Vector2 current = Vector2.Zero, start = Vector2.Zero, lastControl = Vector2.Zero;
        char previous = ' ';

        float Next() => Number(tokens[at++]);
        Vector2 Point(bool relative) { var p = new Vector2(Next(), Next()); return relative ? current + p : p; }
        bool HasNumber() => at < tokens.Count && !char.IsLetter(tokens[at][0]);

        while (at < tokens.Count)
        {
            if (char.IsLetter(tokens[at][0]))
                command = tokens[at++][0];
            else if (previous == ' ')
                break;

            bool relative = char.IsLower(command);
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    current = Point(relative);
                    start = current;
                    shape = new List<Vector2> { current };
                    shapes.Add(shape);
                    command = relative ? 'l' : 'L';
                    break;
                case 'L':
                    current = Point(relative);
                    shape?.Add(current);
                    break;
                case 'H':
                    current = new Vector2(relative ? current.X + Next() : Next(), current.Y);
                    shape?.Add(current);
                    break;
                case 'V':
                    current = new Vector2(current.X, relative ? current.Y + Next() : Next());
                    shape?.Add(current);
                    break;
                case 'C':
                case 'S':
                {
                    bool smooth = char.ToUpperInvariant(command) == 'S';
                    Vector2 c1 = smooth
                        ? ("CS".Contains(char.ToUpperInvariant(previous)) ? 2 * current - lastControl : current)
                        : Point(relative);
                    Vector2 c2 = Point(relative);
                    Vector2 end = Point(relative);
                    Cubic(shape, current, c1, c2, end);
                    lastControl = c2;
                    current = end;
                    break;
                }
                case 'Q':
                case 'T':
                {
                    bool smooth = char.ToUpperInvariant(command) == 'T';
                    Vector2 c = smooth
                        ? ("QT".Contains(char.ToUpperInvariant(previous)) ? 2 * current - lastControl : current)
                        : Point(relative);
                    Vector2 end = Point(relative);
                    Cubic(shape, current, current + 2f / 3f * (c - current), end + 2f / 3f * (c - end), end);
                    lastControl = c;
                    current = end;
                    break;
                }
                case 'A':
                {
                    for (int i = 0; i < 5; i++)
                        Next();
                    current = Point(relative);
                    shape?.Add(current);
                    break;
                }
                case 'Z':
                    current = start;
                    shape = null;
                    break;
                default:
                    throw new XafFormatException($"Unknown SVG path command '{command}'.");
            }
            previous = command;
            if (char.ToUpperInvariant(command) == 'Z' && HasNumber())
                break;
        }

        // A shape that ends where it starts closes on its own.
        foreach (List<Vector2> s in shapes)
        {
            if (s.Count > 1 && s[0].DistanceTo(s[^1]) < 1e-3f)
                s.RemoveAt(s.Count - 1);
        }
        return shapes;
    }

    private static void Cubic(List<Vector2>? shape, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        if (shape is null)
            return;
        float length = a.DistanceTo(b) + b.DistanceTo(c) + c.DistanceTo(d);
        int steps = Math.Max(2, (int)MathF.Ceiling(length / CurveStep));
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps, u = 1 - t;
            shape.Add(u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d);
        }
    }

    private static List<string> Tokens(string data)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < data.Length)
        {
            char ch = data[i];
            if (char.IsLetter(ch) && ch is not ('e' or 'E'))
            {
                tokens.Add(ch.ToString());
                i++;
            }
            else if (char.IsDigit(ch) || ch is '-' or '+' or '.')
            {
                int begin = i++;
                bool dot = ch == '.';
                while (i < data.Length)
                {
                    char next = data[i];
                    if (char.IsDigit(next))
                        i++;
                    else if (next == '.' && !dot)
                    {
                        dot = true;
                        i++;
                    }
                    else if (next is 'e' or 'E')
                    {
                        i++;
                        if (i < data.Length && data[i] is '-' or '+')
                            i++;
                    }
                    else
                        break;
                }
                tokens.Add(data[begin..i]);
            }
            else
            {
                i++;
            }
        }
        return tokens;
    }

    private static float Number(string text) => float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
}
