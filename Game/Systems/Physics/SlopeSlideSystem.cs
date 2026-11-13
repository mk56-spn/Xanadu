using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Physics;

/// <summary>
/// Slides the player down a ramp (<see cref="BlockSlope"/>). On a ramp's
/// surface she is snapped to <see cref="Speed"/> along it, downhill, whatever
/// she landed with: one speed for every slope, never one the mapper picks, so
/// hit timing never carries through a slide. She leaves the bottom with that
/// velocity, and SKID ground keeps its sideways part.
/// </summary>
public sealed class SlopeSlideSystem : QuerySystem<Player, PhysicsBody, PlayfieldView>
{
    /// <summary>How fast every slope slides her, in playfield units per second.</summary>
    public const float Speed = 600f;

    /// <summary>How far, in screen pixels, her low corner may sit off the surface and still be on it.</summary>
    public const float Tolerance = 3f;

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        Query.ForEachEntity((ref player, ref body, ref view, entity) =>
        {
            if (entity.Tags.Has<PlayPaused>() ||
                entity.TryGetComponent(out PlayfieldMode mode) && mode.Kind != PlayfieldKind.Play)
            {
                return;
            }

            Vector2 centre = Box2DPhysicsWorld.ToScreen(body.Body.GetPosition());
            if (!TryDownhill(entity.Store, view, centre, player.Size, body.Body.LinearVelocity, out Vector2 downhill))
                return;

            GroundStepSystem.Cancel(entity, buffer);
            body.Body.SetLinearVelocity(downhill * (Speed * view.Scale / Box2DPhysicsWorld.PixelsPerMeter));
        });
    }

    /// <summary>
    /// True when a player box at <paramref name="centre"/> rests on a ramp's
    /// surface and isn't leaving it; <paramref name="downhill"/> is then the
    /// unit direction down the ramp, in screen space.
    /// </summary>
    public static bool TryDownhill(EntityStore store, PlayfieldView view, Vector2 centre, Vector2 size, Vector2 velocity, out Vector2 downhill)
    {
        foreach (Entity entity in store.Query<Block>().ToEntityList())
        {
            Block block = entity.GetComponent<Block>();
            if (block.Slope == BlockSlope.Off || block.Size.X <= 0 || block.Size.Y <= 0)
                continue;

            Vector2 topLeft = view.PlayfieldToScreen(GroupOffset.Shown(entity, block.Position));
            Vector2[] corners = Block.RampCorners(topLeft, block.Size * view.Scale, block.Slope);
            bool upRight = block.Slope == BlockSlope.UpRight;
            Vector2 low = upRight ? corners[0] : corners[1];
            Vector2 high = corners[2];

            // The box touches a ramp with its uphill bottom corner.
            Vector2 foot = centre + new Vector2(upRight ? size.X / 2 : -size.X / 2, size.Y / 2);
            float left = Math.Min(low.X, high.X), right = Math.Max(low.X, high.X);
            if (foot.X < left - Tolerance || foot.X > right + Tolerance)
                continue;

            Vector2 along = Vector2.Normalize(low - high);
            Vector2 normal = new(along.Y, -along.X);
            if (normal.Y > 0)
                normal = -normal;
            float above = Vector2.Dot(foot - high, normal);
            if (above > Tolerance || above < -size.Y / 2)
                continue;

            // Moving off the surface (a launch) is not sliding.
            if (Vector2.Dot(velocity, normal) > 0.5f)
                continue;

            downhill = along;
            return true;
        }

        downhill = default;
        return false;
    }
}
