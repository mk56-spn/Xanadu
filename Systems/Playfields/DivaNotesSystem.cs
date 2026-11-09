using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// The DIVA+ note look, always on in play (<see cref="DivaNotes"/>). Remembers
/// the moment just hit, so the guide spark knows where to leave from once its
/// notes are deleted (<see cref="DivaSpark"/>).
/// </summary>
public sealed class DivaNotesSystem(EntityStore entities) : QuerySystem<PlayfieldMode>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        PlayfieldRenderer.DivaMoment? next = PlayfieldRenderer.EarliestDivaMoment(entities);

        Query.ForEachEntity((ref PlayfieldMode mode, Entity entity) =>
        {
            if (mode.Kind != PlayfieldKind.Play)
                return;

            if (!entity.Tags.Has<DivaNotes>())
                buffer.AddTag<DivaNotes>(entity.Id);

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
