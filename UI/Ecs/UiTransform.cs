using Friflo.Engine.ECS;
using Godot;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Ecs;

public static class UiTransform
{
    /// <summary>Accumulates parent positions up to the layer entity.</summary>
    public static Vector2 WorldPosition(Entity entity, Vector2 localPosition)
    {
        Vector2 world = localPosition;
        Entity parent = entity.Parent;

        while (!parent.IsNull)
        {
            if (parent.TryGetComponent<Position>(out Position position))
                world += position.Value;

            parent = parent.Parent;
        }

        return world;
    }

    /// <summary>Canvas transform for an entity's local position and rotation.</summary>
    public static Transform2D Local(Entity entity, Vector2 position) =>
        new(entity.TryGetComponent(out UiRotation rotation) ? rotation.Radians : 0, position);
}
