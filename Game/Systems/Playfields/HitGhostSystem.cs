using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>Ages and removes the see-through copies a character leaves when it hits a note.</summary>
public sealed class HitGhostSystem : QuerySystem<HitGhost>
{
    /// <summary>
    /// Leaves a copy of <paramref name="player"/> where it was last drawn. A
    /// player that is not on screen leaves nothing.
    /// </summary>
    public static void Spawn(EntityStore entities, Entity player) => Spawn(entities, player, Vector2.Zero);

    /// <summary>How far past the note a launched copy carries on, in pixels.</summary>
    public const float LaunchOvershoot = 40f;

    /// <summary>
    /// Leaves a copy of <paramref name="player"/> that launches itself at the
    /// note it hit, <paramref name="toNote"/> away (screen pixels), flying a
    /// little past it. The character itself does not move.
    /// </summary>
    public static void Launch(EntityStore entities, Entity player, Vector2 toNote)
    {
        float length = toNote.Length();
        Spawn(entities, player, length < 1f ? Vector2.Zero : toNote * ((length + LaunchOvershoot) / length));
    }

    private static void Spawn(EntityStore entities, Entity player, Vector2 launch)
    {
        if (!player.TryGetComponent(out Player box) ||
            !player.HasComponent<Position>())
        {
            return;
        }

        entities.CreateEntity(new HitGhost(player, PlayerSmoothing.DrawnPosition(player), box.Size) { Launch = launch });
    }

    /// <summary>How much of its launch a copy has flown, <paramref name="t"/> through its life: fast off the mark, easing out.</summary>
    public static float Flown(float t)
    {
        float left = 1f - Mathf.Clamp(t * 1.6f, 0f, 1f);
        return 1f - left * left * left;
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref ghost, entity) =>
        {
            ghost.Age += Tick.deltaTime;
            if (ghost.Age >= ghost.Lifetime || ghost.Player.IsNull)
            {
                buffer.DeleteEntity(entity.Id);
                return;
            }

            ghost.Center = ghost.Start + ghost.Launch * Flown(ghost.Age / ghost.Lifetime);
        });
    }
}
