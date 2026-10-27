using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Replays;
using Xanadu.Settings;

namespace Xanadu.Systems.Replays;

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
        bool replaying = false;

        Query.ForEachEntity((ref player, ref playhead, ref session, entity) =>
        {
            replaying = true;
            if (!session.Started || session.Finished || entity.Tags.Has<PlayPaused>())
                return;

            Advance(ref player, playhead.TimeMs);
        });

        if (!replaying)
        {
            if (keyboard.IsReplaying)
                keyboard.StopReplay();

            return;
        }

        _held.Clear();
        Query.ForEachEntity((ref player, ref _, ref _, _) =>
        {
            for (int i = 0; i < player.Down.Length; i++)
            {
                if (player.Down[i])
                    _held.Add(keybinds.Get(KeybindSettings.Actions[i]));
            }
        });

        keyboard.Replay(_held);
    }

    private static void Advance(ref ReplayPlayer player, long timeMs)
    {
        // A seek backwards, such as a retry on the same entity, replays from the start.
        if (timeMs < player.LastTimeMs)
        {
            player.Cursor = 0;
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
}
