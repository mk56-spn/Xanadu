using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Replays;
using Xanadu.Model.Settings;

namespace Xanadu.Logic.Replays;

/// <summary>
/// Presses the replayed keys on the <see cref="ReplayKeyboard"/> as the song
/// reaches each event. Must run before the systems that read the keyboard.
/// </summary>
public sealed class ReplayPlaybackSystem(KeybindSettings keybinds, ReplayKeyboard keyboard)
    : QuerySystem<ReplayPlayer, Playhead, PlaySession>
{
    private readonly List<Key> _held = new();

    protected override void OnUpdate()
    {
        bool replaying = Query.EachEntity(new Replay(Query.Store)).Replaying;

        if (!replaying)
        {
            if (keyboard.IsReplaying)
                keyboard.StopReplay();

            return;
        }

        _held.Clear();
        Query.Each(new HeldKeys(keybinds, _held));

        keyboard.Replay(_held);
    }

    /// <summary>Judges with the offset the play had at this point, whatever the settings are now.</summary>
    private static void ApplyOffset(ref ReplayPlayer player, long timeMs, Entity entity)
    {
        IReadOnlyList<ReplayOffset>? offsets = player.Replay.Offsets;
        if (offsets is null || offsets.Count == 0 || !entity.HasComponent<PlayOffset>())
            return;

        while (player.OffsetCursor + 1 < offsets.Count && offsets[player.OffsetCursor + 1].TimeMs <= timeMs)
            player.OffsetCursor++;

        entity.GetComponent<PlayOffset>().ReplayTotalMs = offsets[player.OffsetCursor].TotalMs;
    }

    private static void Advance(ref ReplayPlayer player, long timeMs)
    {
        // A seek backwards, such as a retry on the same entity, replays from the start.
        if (timeMs < player.LastTimeMs)
        {
            player.Cursor = 0;
            player.OffsetCursor = 0;
            System.Array.Clear(player.Down);
        }

        player.LastTimeMs = timeMs;
        IReadOnlyList<ReplayEvent> events = player.Replay.Events;
        while (player.Cursor < events.Count && events[player.Cursor].TimeMs <= timeMs)
        {
            ReplayEvent next = events[player.Cursor++];
            player.Down[(int)next.Action] = next.Down;
        }
    }

    private readonly record struct HeldKeys(KeybindSettings Keybinds, List<Key> Held) : IEach<ReplayPlayer, Playhead, PlaySession>
    {
        public void Execute(ref ReplayPlayer player, ref Playhead _, ref PlaySession __)
        {
            for (int i = 0; i < player.Down.Length; i++)
            {
                if (player.Down[i])
                    Held.Add(Keybinds.Get(KeybindSettings.Actions[i]));
            }
        }
    }

    private struct Replay(EntityStore Store) : IEachEntity<ReplayPlayer, Playhead, PlaySession>
    {
        public bool Replaying;

        public void Execute(ref ReplayPlayer player, ref Playhead playhead, ref PlaySession session, int id)
        {
            Entity entity = Store.GetEntityById(id);

            Replaying = true;
            if (!session.Started || session.Finished || entity.Tags.Has<PlayPaused>())
                return;

            Advance(ref player, playhead.TimeMs);
            ApplyOffset(ref player, playhead.TimeMs, entity);
        }
    }
}
