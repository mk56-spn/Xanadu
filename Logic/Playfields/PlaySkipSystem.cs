using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Logic.Playfields;

/// <summary>
/// Offers to skip a song's entrance while more than <see cref="LeadMs"/> remains
/// before the first note, landing that far ahead of it. A recorded skip is
/// repeated by its replay at the same song time, so the player moves as they did.
/// </summary>
public sealed class PlaySkipSystem : QuerySystem<PlaySession, Playhead>
{
    /// <summary>The wait a skip leaves before the first note, and the shortest one worth skipping.</summary>
    public const long LeadMs = 2_000;

    private readonly HashSet<int> _heard = [];

    protected override void OnUpdate()
    {
        foreach (Entity entity in Query.ToEntityList())
        {
            if (_heard.Add(entity.Id))
                entity.AddSignalHandler<SkipAsked>(_ => Asked(entity));
        }

        Query.EachEntity(new Skip(Query.Store, CommandBuffer));
    }

    /// <summary>The skip key (<see cref="SkipAsked"/>): skips now if a player could press SKIP.</summary>
    private static void Asked(Entity entity)
    {
        if (entity.IsNull || !entity.HasComponent<PlaySession>() || !entity.HasComponent<Playhead>())
            return;

        ref PlaySession session = ref entity.GetComponent<PlaySession>();
        ref Playhead playhead = ref entity.GetComponent<Playhead>();
        if (!entity.Tags.Has<PlayPaused>() && !entity.HasComponent<ReplayPlayer>() && CanSkip(session, playhead))
            Apply(entity, ref session, ref playhead);
    }

    private static void Apply(Entity entity, ref PlaySession session, ref Playhead playhead)
    {
        if (entity.HasComponent<ReplayRecorder>())
            entity.GetComponent<ReplayRecorder>().SkipFromMs = playhead.TimeMs;

        session.Skipped = true;
        playhead.SeekRequestMs = session.SkipToMs;
    }

    /// <summary>
    /// Where a skip lands for a run starting at <paramref name="startMs"/>, or -1
    /// when nothing is ahead of it. The caller passes every timed hit object.
    /// </summary>
    public static long SkipTarget(IEnumerable<long> objectTimesMs, long startMs)
    {
        long first = long.MaxValue;
        foreach (long time in objectTimesMs)
        {
            if (time >= startMs)
                first = Math.Min(first, time);
        }

        return first == long.MaxValue ? -1 : first - LeadMs;
    }

    private static bool CanSkip(in PlaySession session, in Playhead playhead) =>
        session.Started &&
        !session.Skipped &&
        !session.Ending &&
        !session.Died &&
        !session.Finished &&
        playhead.SeekRequestMs < 0 &&
        playhead.TimeMs < session.SkipToMs &&
        session.SkipToMs < playhead.DurationMs;

    private static bool ReplayReachedSkip(in ReplayPlayer player, in Playhead playhead) =>
        player.Replay.SkipFromMs >= 0 && playhead.TimeMs >= player.Replay.SkipFromMs;

    private readonly record struct Skip(EntityStore Store, CommandBuffer Buffer) : IEachEntity<PlaySession, Playhead>
    {
        public void Execute(ref PlaySession session, ref Playhead playhead, int id)
        {
            Entity entity = Store.GetEntityById(id);

            bool available = !entity.Tags.Has<PlayPaused>() && CanSkip(session, playhead);
            bool replaying = entity.HasComponent<ReplayPlayer>();
            UiLayout.SetHidden(session.SkipButton, Buffer, !available || replaying);

            if (!available)
                return;

            bool skip = replaying
                ? ReplayReachedSkip(entity.GetComponent<ReplayPlayer>(), playhead)
                : !session.SkipButton.IsNull && session.SkipButton.Tags.Has<Pressed>();

            if (skip)
                Apply(entity, ref session, ref playhead);
        }
    }
}
