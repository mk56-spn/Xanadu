using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Offers to skip a song's entrance while more than <see cref="LeadMs"/> remains
/// before the first note, landing that far ahead of it. A recorded skip is
/// repeated by its replay at the same song time, so the player moves as they did.
/// </summary>
public sealed class PlaySkipSystem : QuerySystem<PlaySession, Playhead>
{
    /// <summary>The wait a skip leaves before the first note, and the shortest one worth skipping.</summary>
    public const long LeadMs = 2_000;

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref session, ref playhead, entity) =>
        {
            bool requested = session.SkipRequested;
            session.SkipRequested = false;

            bool available = !entity.Tags.Has<PlayPaused>() && CanSkip(session, playhead);
            bool replaying = entity.HasComponent<ReplayPlayer>();
            UiLayout.SetHidden(session.SkipButton, buffer, !available || replaying);

            if (!available)
                return;

            bool skip = replaying
                ? ReplayReachedSkip(entity.GetComponent<ReplayPlayer>(), playhead)
                : requested || (!session.SkipButton.IsNull && session.SkipButton.Tags.Has<Pressed>());

            if (!skip)
                return;

            if (entity.HasComponent<ReplayRecorder>())
                entity.GetComponent<ReplayRecorder>().SkipFromMs = playhead.TimeMs;

            session.Skipped = true;
            playhead.SeekRequestMs = session.SkipToMs;
        });
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
}
