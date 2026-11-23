using System.Collections.Generic;

namespace Xanadu.Model.Animation;

/// <summary>Point tests against a part's outline: the polygons of its geometry.</summary>
public static class RigOutline
{
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

    /// <summary>A geometry's polygons with at least three points, as arrays.</summary>
    public static List<Vector2[]> Polygons(Core.Items.XifGeometry? geometry)
    {
        var polygons = new List<Vector2[]>();
        if (geometry is null)
            return polygons;
        foreach (Core.Items.XifPolygon polygon in geometry.Polygons)
        {
            if (polygon.Points.Count >= 3)
                polygons.Add([.. polygon.Points]);
        }
        return polygons;
    }
}
