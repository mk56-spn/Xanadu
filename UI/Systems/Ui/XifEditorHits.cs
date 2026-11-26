using System.Collections.Generic;
using Xanadu.Core;
using Xanadu.Core.Items;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>What the pointer is on in the XIF editor's shape box: a point, an edge or a shape, and where a drag snaps.</summary>
public static class XifEditorHits
{
    private const float GrabRadius = XifEditorSystem.GrabRadius;
    private const float EdgeRadius = XifEditorSystem.EdgeRadius;

    public static int NearestPoint(XifPolygon polygon, Transform2D toScreen, Vector2 pointer)
    {
        int nearest = -1;
        float best = GrabRadius * GrabRadius;
        List<Vector2> handles = XifEditing.Handles(polygon);
        for (int i = 0; i < handles.Count; i++)
        {
            float distance = (toScreen * handles[i]).DistanceSquaredTo(pointer);
            if (distance > best)
                continue;
            best = distance;
            nearest = i;
        }
        return nearest;
    }

    /// <summary>The edge of <paramref name="polygon"/> under the pointer: the point it starts at, and the spot on it nearest the pointer, on screen.</summary>
    public static (int After, Vector2 Screen)? NearestEdge(XifPolygon polygon, Transform2D toScreen, Vector2 pointer)
    {
        (int, Vector2)? found = null;
        float best = EdgeRadius * EdgeRadius;
        int count = polygon.Points.Count;
        for (int i = 0; i < count && count >= 2; i++)
        {
            Vector2 a = toScreen * polygon.Points[i];
            Vector2 b = toScreen * polygon.Points[(i + 1) % count];
            Vector2 along = b - a;
            float length = along.LengthSquared();
            if (length < 1f)
                continue;
            float t = Math.Clamp((pointer - a).Dot(along) / length, 0f, 1f);
            // Close to either end it is the point's to grab, not the edge's.
            if (t < 0.08f || t > 0.92f)
                continue;
            Vector2 spot = a + along * t;
            float distance = spot.DistanceSquaredTo(pointer);
            if (distance > best)
                continue;
            best = distance;
            found = (i, spot);
        }
        return found;
    }

    /// <summary>The topmost shape holding an item-space point, or -1.</summary>
    public static int ShapeAt(Xif item, Vector2 point)
    {
        for (int i = item.Polygons.Count - 1; i >= 0; i--)
        {
            if (Holds(item.Polygons[i].Points, point))
                return i;
        }
        return -1;
    }

    /// <summary>Even-odd: whether a ray from the point crosses the outline an odd number of times.</summary>
    public static bool Holds(List<Vector2> outline, Vector2 point)
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

    /// <summary>Whole units unless Shift is held, so outlines stay tidy.</summary>
    public static Vector2 Snap(Vector2 point, bool free) => free ? point : point.Round();
}
