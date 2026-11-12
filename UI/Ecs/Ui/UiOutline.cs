
namespace Xanadu.Ecs;

/// <summary>A cut across the top right corner of a rectangle or container, this many pixels along each side.</summary>
public struct UiChamfer(float size) : Friflo.Engine.ECS.IComponent
{
    public float Size = size;
}

/// <summary>The outline of a rectangle after skewing it and cutting its top right corner.</summary>
public static class UiOutline
{
    /// <summary>
    /// Corners clockwise from the top left. A skew leans the top edge right of
    /// the bottom by that angle; a chamfer replaces the top right corner with
    /// two points, which makes five.
    /// </summary>
    public static Vector2[] Points(Rect2 bounds, float skew, float chamfer)
    {
        float shear = Mathf.Tan(skew) * bounds.Size.Y / 2;
        Vector2 topLeft = new(bounds.Position.X + shear, bounds.Position.Y);
        Vector2 topRight = new(bounds.End.X + shear, bounds.Position.Y);
        Vector2 bottomRight = new(bounds.End.X - shear, bounds.End.Y);
        Vector2 bottomLeft = new(bounds.Position.X - shear, bounds.End.Y);
        if (chamfer <= 0)
            return [topLeft, topRight, bottomRight, bottomLeft];

        // Cut along the top edge and down the right edge, which may lean.
        float cut = Mathf.Min(chamfer, Mathf.Min(bounds.Size.X, bounds.Size.Y));
        Vector2 down = (bottomRight - topRight).Normalized();
        return
        [
            topLeft,
            new Vector2(topRight.X - cut, topRight.Y),
            topRight + down * cut,
            bottomRight,
            bottomLeft,
        ];
    }
}
