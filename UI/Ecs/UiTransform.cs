using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Tweens;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Ecs;

/// <summary>
/// Where UI entities are. Two answers: <b>laid out</b> (<see cref="WorldPosition"/>),
/// where the containers put them, which layout code reads and writes; and
/// <b>shown</b> (<see cref="ShownPosition"/>, <see cref="Local"/>), with every
/// tween on the way down applied, which drawing and hit tests read. Tweens never
/// leak into layout, so a moving thing never pushes its container around.
/// </summary>
public static class UiTransform
{
    /// <summary>Accumulates parent positions up to the layer entity: the laid-out place, tweens left out.</summary>
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

    /// <summary>
    /// Where the entity's origin shows: its laid-out place (<paramref name="localPosition"/>
    /// is its own <see cref="Position"/>) plus the tween offsets of it and every
    /// parent. Turns and scales are left out, as rotation always was for hit tests.
    /// </summary>
    public static Vector2 ShownPosition(Entity entity, Vector2 localPosition)
    {
        Vector2 world = localPosition + UiTween.Pose(entity).Offset;
        for (Entity parent = entity.Parent; !parent.IsNull; parent = parent.Parent)
        {
            if (parent.TryGetComponent<Position>(out Position position))
                world += position.Value + UiTween.Pose(parent).Offset;
        }

        return world;
    }

    /// <summary>
    /// Canvas transform for an entity's local position and rotation, with its
    /// tweens applied on top: turned and scaled about its box's centre, then slid.
    /// </summary>
    public static Transform2D Local(Entity entity, Vector2 position)
    {
        var placed = new Transform2D(entity.TryGetComponent(out UiRotation rotation) ? rotation.Radians : 0, position);
        TweenPose pose = UiTween.Pose(entity);
        if (pose.IsIdentity)
            return placed;

        Vector2 pivot = entity.TryGetComponent(out Rectangle rect) ? rect.Bounds.GetCenter() : Vector2.Zero;
        return placed * pose.Around(pivot);
    }
}
