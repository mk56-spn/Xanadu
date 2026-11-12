using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Kills the player the moment their damage box touches a spike's hitbox. Spikes
/// are permanent geometry, so there is no timing window: contact is death, and
/// <see cref="PlaySession.Died"/> lets the session system end the run and the
/// scene system send the player back to the start. On contact the player bursts
/// into particles and vanishes. Mines keep their own accuracy-penalty handling.
/// </summary>
public sealed class SpikeContactSystem : QuerySystem<Player, Position, PlayfieldView, PlaySession>
{
    private readonly EntityStore _entities;

    public SpikeContactSystem(EntityStore entities)
    {
        _entities = entities;
    }

    protected override void OnUpdate()
    {
        foreach (Entity playfield in Query.ToEntityList())
        {
            if (playfield.Tags.Has<PlayPaused>())
            {
                return;
            }

            ref PlaySession session = ref playfield.GetComponent<PlaySession>();
            if (!session.Started || session.Finished || session.Died)
                return;

            Player player = playfield.GetComponent<Player>();
            Vector2 center = playfield.GetComponent<Position>().Value;
            Rect2 playerRect = PlayerTuck.Box(playfield, center);
            PlayfieldView view = playfield.GetComponent<PlayfieldView>();

            foreach (Entity entity in _entities.Query<Spike>().ToEntityList())
            {
                SpikeTriangle hitbox = SpikeRules.ScreenHitbox(
                    entity.GetComponent<Spike>(),
                    GroupOffset.View(entity, view));
                if (hitbox.Overlaps(playerRect))
                {
                    session.Died = true;
                    ShaderPlay.Mark(ShaderEvent.Death);
                    Shatter(playfield, player.Size);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// The player bursts into particles where it stands and is gone: its body
    /// leaves the simulation so nothing carries it on while the death plays.
    /// </summary>
    private void Shatter(Entity playfield, Vector2 size)
    {
        _entities.CreateEntity(new ParticleBurst(
            PlayerSmoothing.DrawnPosition(playfield),
            GameParticles.Shatter,
            PlayfieldRenderer.PlayerColor,
            size));

        if (playfield.TryGetComponent(out PhysicsBody body))
        {
            body.Body.SetLinearVelocity(System.Numerics.Vector2.Zero);
            body.Body.IsEnabled = false;
        }

        CommandBuffer.AddTag<PlayerShattered>(playfield.Id);
    }
}
