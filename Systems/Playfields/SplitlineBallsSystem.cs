using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>Lets Splitline's two balls trail the character with a soft lerp.</summary>
public sealed class SplitlineBallsSystem : QuerySystem<SplitlineBalls, Position>
{
    private const float Follow = 0.18f;
    private const float Spacing = 26f;

    protected override void OnUpdate()
    {
        float delta = Math.Min(1f, Tick.deltaTime * 60f) * Follow;

        Query.ForEachEntity((ref balls, ref position, _) =>
        {
            Vector2 left = position.Value + new Vector2(-Spacing, -6f);
            Vector2 right = position.Value + new Vector2(Spacing, -6f);
            balls.Left = balls.Left == Vector2.Zero
                ? left
                : balls.Left.Lerp(left, delta);
            balls.Right = balls.Right == Vector2.Zero
                ? right
                : balls.Right.Lerp(right, delta);
        });
    }
}
