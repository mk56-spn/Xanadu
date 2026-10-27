using Friflo.Engine.ECS;
using Godot;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Ecs;

public sealed partial class UiBuilder
{
    /// <summary>
    /// Creates a polygon or polyline drawable. Points are relative to
    /// <paramref name="position"/>, which is also the pivot for
    /// <paramref name="rotation"/>; with neither, artwork authored in
    /// screen coordinates can be used as is.
    /// </summary>
    public Entity ShapeEntity(
        Vector2[] points,
        Color fill,
        Color stroke = default,
        float strokeWidth = 0,
        bool closed = true,
        Vector2? position = null,
        float rotation = 0)
    {
        RequireLayer();

        Vector2 min = points[0];
        Vector2 max = points[0];
        foreach (Vector2 point in points)
        {
            min = min.Min(point);
            max = max.Max(point);
        }

        Entity shape = _entities.CreateEntity(
            new VisualRid(_visuals.CreateCanvasItem()),
            new Rectangle(new Rect2(min, max - min)),
            new Drawable(_layerZIndex),
            new Position(position ?? Vector2.Zero),
            new UiShape(points, fill, stroke, strokeWidth, closed),
            Tags.Get<Uninitialised, Dirty>());
        if (rotation != 0)
            shape.AddComponent(new UiRotation(rotation));

        Attach(shape);
        return shape;
    }
}
