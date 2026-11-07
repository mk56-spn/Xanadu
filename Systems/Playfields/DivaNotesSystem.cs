using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// F8: the DIVA+ note look in play, on by default (<see cref="DivaNotes"/>).
/// While it is on, remembers the moment just hit, so the guide spark knows
/// where to leave from once its notes are deleted (<see cref="DivaSpark"/>).
/// </summary>
public sealed class DivaNotesSystem(EntityStore entities, IOverlayToggleSource toggle) : QuerySystem<PlayfieldMode>
{
    private readonly OverlayLatch _latch = new(toggle);

    protected override void OnUpdate()
    {
        _latch.Update();
        // The latch starts closed; closed is DIVA+, so F8 brings the ring back.
        bool diva = !_latch.Visible;
        CommandBuffer buffer = CommandBuffer;
        PlayfieldRenderer.DivaMoment? next = diva ? PlayfieldRenderer.EarliestDivaMoment(entities) : null;

        Query.ForEachEntity((ref PlayfieldMode mode, Entity entity) =>
        {
            if (mode.Kind != PlayfieldKind.Play)
                return;

            if (entity.Tags.Has<DivaNotes>() != diva)
            {
                if (diva)
                    buffer.AddTag<DivaNotes>(entity.Id);
                else
                    buffer.RemoveTag<DivaNotes>(entity.Id);
            }

            if (!diva)
                return;

            if (entity.HasComponent<DivaSpark>())
                Track(ref entity.GetComponent<DivaSpark>(), next);
            else
            {
                var spark = new DivaSpark();
                Track(ref spark, next);
                buffer.AddComponent(entity.Id, spark);
            }
        });
    }

    /// <summary>
    /// When the earliest moment left moves on to a later one, the one before it
    /// was hit: it becomes where the spark leaves from. A moment earlier than
    /// the one held means play went back, so there is nothing to leave from.
    /// </summary>
    private static void Track(ref DivaSpark spark, PlayfieldRenderer.DivaMoment? next)
    {
        if (next is not { } moment)
            return;

        if (spark.HasNext && moment.TimeMs == spark.NextMs)
            return;

        if (spark.HasNext && moment.TimeMs > spark.NextMs)
        {
            spark.HasFrom = true;
            spark.FromMs = spark.NextMs;
            spark.From = spark.Next;
            spark.FromChord = spark.NextChord;
        }
        else
        {
            spark.HasFrom = false;
        }

        spark.HasNext = true;
        spark.NextMs = moment.TimeMs;
        spark.Next = moment.Anchor;
        spark.NextChord = moment.Count > 1;
    }
}
