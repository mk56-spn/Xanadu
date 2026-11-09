using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Physics;

/// <summary>
/// Turns the ground's friction on when the player crosses a friction bar, and
/// keeps the body's own friction in step with it before each physics frame.
/// Only movement crosses a bar: a dash or a wrap round the field jumps past
/// without touching it. While play runs, every bar gives off embers.
/// </summary>
public sealed class FrictionBarSystem(Box2DPhysicsWorld physics)
    : QuerySystem<Player, PhysicsBody, Position, PlayfieldView>
{
    /// <summary>Seconds between one round of embers and the next.</summary>
    public const float EmberSeconds = 0.18f;

    /// <summary>Pixels of slack on the distance velocity explains, for rounding and the floor's clamp.</summary>
    public const float CrossSlack = 2f;

    private float _sinceEmbers;

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        bool embers = false;
        _sinceEmbers += delta;
        if (_sinceEmbers >= EmberSeconds)
        {
            _sinceEmbers = 0f;
            embers = true;
        }

        foreach (Entity entity in Query.ToEntityList())
        {
            if (entity.Tags.Has<PlayPaused>())
                continue;

            Vector2 centre = entity.GetComponent<Position>().Value;
            if (!entity.HasComponent<PlayerFriction>())
                entity.AddComponent(new PlayerFriction { LastX = centre.X });

            ref PlayerFriction friction = ref entity.GetComponent<PlayerFriction>();
            PlayfieldView view = entity.GetComponent<PlayfieldView>();
            Box2DSharp.Dynamics.Body body = entity.GetComponent<PhysicsBody>().Body;
            float speed = Math.Abs(body.LinearVelocity.X) * Box2DPhysicsWorld.PixelsPerMeter;
            bool moved = Math.Abs(centre.X - friction.LastX) <= speed * delta * 2f + CrossSlack;

            if (moved && TryCross(entity, view, friction.LastX, centre, out Entity crossed))
            {
                friction.On = true;
                Burst(crossed, view, amount: 3);
            }

            friction.LastX = centre.X;
            physics.SetFriction(body, friction.On ? PlayerBodySystem.CharacterFriction : 0f);

            if (embers)
            {
                foreach (Entity bar in entity.Store.Query<FrictionBar>().ToEntityList())
                    Burst(bar, view, amount: 1);
            }
        }
    }

    /// <summary>
    /// The bar whose line lies between <paramref name="lastX"/> and the
    /// player's centre now, with the player's box overlapping its height.
    /// </summary>
    public static bool TryCross(Entity player, PlayfieldView view, float lastX, Vector2 centre, out Entity crossed)
    {
        Vector2 half = player.GetComponent<Player>().Size / 2;
        foreach (Entity entity in player.Store.Query<FrictionBar>().ToEntityList())
        {
            FrictionBar bar = entity.GetComponent<FrictionBar>();
            Vector2 top = view.PlayfieldToScreen(GroupOffset.Shown(entity, new Vector2(bar.X, bar.Position.Y)));
            float bottom = top.Y + bar.Height * view.Scale;
            bool across = (lastX < top.X) != (centre.X < top.X);
            bool level = centre.Y - half.Y < bottom && centre.Y + half.Y > top.Y;
            if (across && level)
            {
                crossed = entity;
                return true;
            }
        }

        crossed = default;
        return false;
    }

    private static void Burst(Entity bar, PlayfieldView view, int amount)
    {
        Rect2 screen = PlayfieldRenderer.FrictionBarScreenBounds(bar.GetComponent<FrictionBar>(), GroupOffset.View(bar, view));
        Color tint = GroupColor.For(bar, PlayfieldRenderer.FrictionBarColor);
        for (int i = 0; i < amount; i++)
            bar.Store.CreateEntity(new ParticleBurst(screen.GetCenter(), ParticleKind.Ember, tint, screen.Size));
    }
}
