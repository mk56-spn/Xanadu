using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Timeline;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;

namespace Xanadu.Model.Editor;

/// <summary>
/// The note the current tool places, from the editor's tool state. Placing and
/// the ghost under the pointer share it, so the ghost always tells the truth.
/// </summary>
public static class EditorPlacementNote
{
    /// <summary>A dash dragged shorter than this gets <see cref="DefaultDashLength"/>, playfield units.</summary>
    public const float MinDrag = 20f;

    /// <summary>A dash's length when it is clicked rather than dragged, playfield units.</summary>
    public const float DefaultDashLength = 300f;

    /// <summary>Where a hold's tail sits when it is clicked rather than dragged, from the head.</summary>
    public static readonly Vector2 DefaultHoldTail = new(PlayfieldMetrics.NoteSize * 3f, 0f);

    /// <summary>The tool's note at a time and place; <paramref name="dragTo"/> is where a dash or hold was dragged to, if anywhere.</summary>
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
            case EditorPlacementMode.Hold:
                TempoMap? tempo = editor.TryGetComponent(out Playhead playhead) ? playhead.Tempo : null;
                return note with { Hold = new NoteHold(HoldTailMs(tempo, time, placement.HoldBeats), dragged ? drag : DefaultHoldTail) };
            default:
                return note;
        }
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
