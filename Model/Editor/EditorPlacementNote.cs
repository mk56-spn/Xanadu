using System;
using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Model.Levels;

namespace Xanadu.Model.Editor;

/// <summary>
/// The note the current tool places, from the editor's tool state. Placing and
/// the ghost under the pointer share it, so the ghost always tells the truth.
/// </summary>
public static class EditorPlacementNote
{
    /// <summary>A dash dragged shorter than this gets <see cref="DefaultDashLength"/>, a hold tail this close to its head <see cref="DefaultHoldTail"/>; playfield units.</summary>
    public const float MinDrag = 20f;

    /// <summary>A dash's length when it is clicked rather than dragged, playfield units.</summary>
    public const float DefaultDashLength = 300f;

    /// <summary>Where a hold's tail sits, from the head, when it is ended with Enter or clicked on its own head.</summary>
    public static readonly Vector2 DefaultHoldTail = new(PlayfieldMetrics.NoteSize * 3f, 0f);

    /// <summary>
    /// The tool's note at a time and place; <paramref name="dragTo"/> is where a dash was dragged to, if anywhere.
    /// A hold's tail is not known until it is laid (<see cref="BuildHold"/>), so here it is just its head.
    /// </summary>
    public static Note Build(Entity editor, long time, Vector2 position, Vector2? dragTo = null)
    {
        EditorPlacement placement = editor.TryGetComponent(out EditorPlacement found)
            ? found
            : new EditorPlacement();
        var note = new Note(time, position, placement.Note);
        Vector2 drag = dragTo is { } to ? to - position : Vector2.Zero;
        bool dragged = drag.Length() >= MinDrag;

        switch (placement.Mode)
        {
            case EditorPlacementMode.Dash:
                return note with { DashLength = dragged ? drag.Length() : DefaultDashLength };
            default:
                return note;
        }
    }

    /// <summary>The tool's hold note, its tail <paramref name="tailMs"/> after the head at <paramref name="tail"/> from it.</summary>
    public static Note BuildHold(Entity editor, long time, Vector2 position, long tailMs, Vector2 tail)
    {
        Note note = Build(editor, time, position);
        Vector2 spot = tail.Length() >= MinDrag ? tail : DefaultHoldTail;
        return note with { Hold = new NoteHold(Math.Max(NoteHold.MinTailMs, tailMs), spot) };
    }

    /// <summary>
    /// Whether the tool could place right now: there is a song to time it to and
    /// the level is not full. The ghost shows red when not, as osu!'s placements do.
    /// A route being laid also turns red once it has all the turns it may hold.
    /// </summary>
    public static bool CanPlace(Entity editor, EntityStore entities) =>
        editor.TryGetComponent(out LevelInfo level) && level.Song is not null &&
        !EditorItems.IsFull(entities) &&
        !(editor.TryGetComponent(out EditorSpanDraft draft) && draft.Active &&
          draft.Mode == EditorPlacementMode.Curve && draft.TimesMs.Count >= NoteRoute.MaxTimes);

    /// <summary>
    /// Where a route or hold being laid would end if finished now, ms after its head:
    /// the snapped playhead, but never less than one snap step for a hold, or for a
    /// route with no turns yet (as osu!'s spinner is never shorter than its beat).
    /// A route with turns ends at its last one when the playhead is not past it.
    /// </summary>
    public static long SpanEndMs(Playhead playhead, in EditorSpanDraft draft)
    {
        long at = EditorTiming.Snap(playhead.TimeMs, playhead) - draft.StartMs;
        long last = draft.TimesMs.Count > 0 ? draft.TimesMs[^1] : 0;
        if (last > 0)
            return Math.Max(at, last);
        long step = EditorTiming.Step(draft.StartMs, playhead, 1) - draft.StartMs;
        return Math.Max(at, Math.Max(NoteHold.MinTailMs, step));
    }

    /// <summary>How long, ms, a hold of <paramref name="beats"/> from <paramref name="headMs"/> lasts on the song's tempo.</summary>
    public static long HoldTailMs(TempoMap? tempo, long headMs, double beats)
    {
        double ms = tempo is null
            ? beats * TempoMap.BeatMs(120)
            : tempo.TimeAt(tempo.BeatAt(headMs) + beats) - headMs;
        return Math.Max(NoteHold.MinTailMs, (long)Math.Round(ms));
    }

    /// <summary>How many beats a hold from <paramref name="headMs"/> lasting <paramref name="tailMs"/> covers.</summary>
    public static double HoldBeats(TempoMap? tempo, long headMs, long tailMs) =>
        tempo is null
            ? tailMs / TempoMap.BeatMs(120)
            : tempo.BeatAt(headMs + tailMs) - tempo.BeatAt(headMs);
}
