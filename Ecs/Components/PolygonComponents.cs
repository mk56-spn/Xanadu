using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Items;

namespace Xanadu.Ecs;

/// <summary>
/// A filled shape drawn into a level: a <see cref="Xif"/> of polygons whose
/// outlines are in its own space around <see cref="Position"/>, turned by
/// <see cref="RotationDegrees"/>. It has no timing and no collision; groups
/// fade and move it like any other item. The shared XIF editor edits its item.
/// </summary>
public struct LevelPolygon(Vector2 position, Xif item, float rotationDegrees = 0f) : IComponent
{
    /// <summary>The item's origin on the playfield, in playfield units.</summary>
    public Vector2 Position = position;

    /// <summary>How far the shape is turned about its origin, clockwise, in degrees.</summary>
    public float RotationDegrees = rotationDegrees;

    /// <summary>The shape. Shared by reference, so anything that keeps a copy clones it.</summary>
    public Xif Item = item;

    /// <summary>A primitive's outline on the playfield.</summary>
    public readonly Vector2[] Outline(XifPrimitive primitive)
    {
        var points = new Vector2[primitive.Points.Count];
        float angle = Mathf.DegToRad(RotationDegrees);
        for (int i = 0; i < points.Length; i++)
            points[i] = Position + primitive.Points[i].Rotated(angle);
        return points;
    }

    /// <summary>The upright box around every outline, on the playfield.</summary>
    public readonly Rect2 Bounds
    {
        get
        {
            var bounds = new Rect2(Position, Vector2.Zero);
            if (Item is null)
                return bounds;

            foreach (XifPrimitive primitive in Item.Primitives)
            {
                foreach (Vector2 point in Outline(primitive))
                    bounds = bounds.Expand(point);
            }

            return bounds;
        }
    }

    /// <summary>The box the shape fills in its own space, unturned, placed on the playfield; what rotation sizes it by.</summary>
    public readonly Vector2 LocalSize => Item is null ? Vector2.Zero : XifEditing.Bounds(Item).Size;

    /// <summary>Whether a playfield point lies inside any of its polygons.</summary>
    public readonly bool Contains(Vector2 point)
    {
        if (Item is null)
            return false;

        foreach (XifPrimitive primitive in Item.Primitives)
        {
            if (primitive.IsPolygon && primitive.Points.Count >= 3 && TurnedBox.InsideOutline(Outline(primitive), point))
                return true;
        }

        return false;
    }

    /// <summary>A polygon item made from a playfield outline: its origin is the outline's centre.</summary>
    public static LevelPolygon FromOutline(IReadOnlyList<Vector2> outline, Color fill)
    {
        var box = new Rect2(outline[0], Vector2.Zero);
        foreach (Vector2 point in outline)
            box = box.Expand(point);
        Vector2 origin = box.GetCenter();

        var local = new List<Vector2>(outline.Count);
        foreach (Vector2 point in outline)
            local.Add(point - origin);

        var item = new Xif();
        int index = XifEditing.AddPolygon(item, local);
        item.Primitives[index].Fill = fill;
        return new LevelPolygon(origin, item);
    }
}

/// <summary>
/// The outline the polygon tool is laying down, on the editor entity: one
/// point per click until it is closed into a <see cref="LevelPolygon"/>.
/// </summary>
public struct EditorPolygonDraft() : IComponent
{
    public List<Vector2> Points = new();

    /// <summary>Set by Enter and Esc; the input system acts on them next frame.</summary>
    public bool FinishRequested;
    public bool CancelRequested;

    public readonly bool Active => Points is { Count: > 0 };
}
