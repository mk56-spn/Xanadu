using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Kills the player the moment their damage box touches a spike tile. Spikes
/// are permanent geometry, so there is no timing window: contact is death, and
/// <see cref="PlaySession.Died"/> lets the session system end the run as a
/// failure. Mines keep their own accuracy-penalty handling.
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
                return;

            ref PlaySession session = ref playfield.GetComponent<PlaySession>();
            if (!session.Started || session.Finished || session.Died)
                return;

            Player player = playfield.GetComponent<Player>();
            Vector2 center = playfield.GetComponent<Position>().Value;
            var playerRect = new Rect2(
                center - player.Size / 2,
                player.Size);
            PlayfieldView view = playfield.GetComponent<PlayfieldView>();

            foreach (Entity entity in _entities.Query<Spike>().ToEntityList())
            {
                Rect2 bounds = SpikeRules.ScreenBounds(
                    entity.GetComponent<Spike>(),
                    view);
                if (playerRect.Intersects(bounds))
                {
                    session.Died = true;
                    return;
                }
            }
        }
    }
}
