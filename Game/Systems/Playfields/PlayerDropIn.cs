using System;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Systems.Physics;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// The player drops into the level rather than hanging in the void. She is
/// hidden until a short drop before the first note, then fades in already
/// falling, placed above the note so the fall lands her on it at its time.
/// The drop is worked out from the note (its place and time), and the speed is
/// set rather than built up, so every run arrives the same way.
/// </summary>
public static class PlayerDropIn
{
    /// <summary>How long the visible drop lasts before the first note.</summary>
    public const float DropSeconds = 0.4f;

    /// <summary>How long the fade-in takes once the drop starts.</summary>
    public const float FadeSeconds = 0.2f;

    /// <summary>The downward speed, in metres per second, an aimed drop starts with.</summary>
    public const float EntrySpeed = 4f;

    /// <summary>
    /// Aims the drop at the first note at or after <paramref name="startMs"/>:
    /// where she appears, in screen pixels, and when. False when there is no
    /// note ahead.
    /// </summary>
    public static bool Aim(
        LevelDocument document,
        Entity playfield,
        long startMs,
        out Vector2 spawn,
        out long dropStartMs)
    {
        spawn = default;
        dropStartMs = startMs;
        NoteDocument? first = document.Notes
            .Where(note => note.TimeMs >= startMs)
            .MinBy(note => note.TimeMs);
        if (first is null)
            return false;

        PlayfieldView view = playfield.GetComponent<PlayfieldView>();
        Vector2 placed = new(first.X, first.Y);
        if (playfield.TryGetComponent(out LevelGroups groups))
        {
            placed += GroupModulation.Offset(
                groups.Groups,
                ItemGroups.FromList(first.Groups),
                first.TimeMs / 1000f,
                playfield.GetComponent<Playhead>().Tempo);
        }

        float seconds = Math.Min(DropSeconds, (first.TimeMs - startMs) / 1000f);
        dropStartMs = first.TimeMs - (long)(seconds * 1000f);
        spawn = view.PlayfieldToScreen(placed) - new Vector2(0f, Fallen(EntrySpeed, seconds));
        return true;
    }

    /// <summary>How far, in screen pixels, a body starting down at <paramref name="speed"/> m/s falls in <paramref name="seconds"/>.</summary>
    public static float Fallen(float speed, float seconds) =>
        (speed * seconds + 0.5f * Box2DPhysicsWorld.DefaultGravity * seconds * seconds) *
        Box2DPhysicsWorld.PixelsPerMeter;

    /// <summary>Whether the drop has begun: the song is playing and its time has come.</summary>
    public static bool Begun(Entity entity)
    {
        if (!entity.TryGetComponent(out PlaySession session))
            return true;

        return session.Started &&
               (!entity.TryGetComponent(out Playhead playhead) || playhead.TimeMs >= session.DropStartMs);
    }

    /// <summary>How far into the drop the song is, in seconds; 0 before it.</summary>
    public static float Elapsed(Entity entity) =>
        entity.TryGetComponent(out PlaySession session) && entity.TryGetComponent(out Playhead playhead)
            ? Math.Max(0f, (playhead.TimeMs - session.DropStartMs) / 1000f)
            : 0f;

    /// <summary>How visible the player is: 0 before the drop, rising to 1 over <see cref="FadeSeconds"/>.</summary>
    public static float Alpha(Entity entity)
    {
        if (!entity.HasComponent<PlaySession>())
            return 1f;

        if (!Begun(entity))
            return 0f;

        return Math.Clamp(Elapsed(entity) / FadeSeconds, 0f, 1f);
    }
}
