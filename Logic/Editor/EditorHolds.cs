using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor;
using Xanadu.Logic.Editor.History;

namespace Xanadu.Logic.Editor;

/// <summary>
/// Hold lengths in the editor: the selected holds step longer or shorter by one
/// snap step (one undo step), and the readout gives the first selected hold's length.
/// </summary>
public static class EditorHolds
{
    /// <summary>Steps every selected hold by <paramref name="direction"/> snap steps, landing on the snap. False when none is selected.</summary>
    public static bool Step(Entity editor, int direction, EditorHistory history)
    {
        if (!editor.HasComponent<Playhead>())
            return false;

        List<Entity> holds = Selected(editor);
        if (holds.Count == 0)
            return false;

        Playhead playhead = editor.GetComponent<Playhead>();
        double step = EditorTiming.StepBeats(playhead);
        EditorSnapshot before = history.Capture();
        foreach (Entity hold in holds)
        {
            ref Note note = ref hold.GetComponent<Note>();
            NoteHold tail = note.Hold!.Value;
            double beats = Stepped(EditorPlacementNote.HoldBeats(playhead.Tempo, note.TimeMs, tail.TailMs), direction, step);
            note.Hold = tail with { TailMs = EditorPlacementNote.HoldTailMs(playhead.Tempo, note.TimeMs, beats) };
        }

        history.Record(before, history.Capture());
        return true;
    }

    /// <summary>The first selected hold's length, as the readout shows it; "-" with none selected.</summary>
    public static string Label(Entity editor)
    {
        List<Entity> holds = Selected(editor);
        if (holds.Count == 0 || !editor.HasComponent<Playhead>())
            return "-";

        Note note = holds[0].GetComponent<Note>();
        double beats = Math.Round(EditorPlacementNote.HoldBeats(editor.GetComponent<Playhead>().Tempo, note.TimeMs, note.Hold!.Value.TailMs), 2);
        return beats == 1d ? "1 BEAT" : $"{beats:0.##} BEATS";
    }

    private static List<Entity> Selected(Entity editor)
    {
        var holds = new List<Entity>();
        if (!editor.TryGetComponent(out NoteSelection selection))
            return holds;
        foreach (Entity entity in selection.AllNotes)
        {
            if (!entity.IsNull && entity.TryGetComponent(out Note note) && note.IsHold)
                holds.Add(entity);
        }

        return holds;
    }

    /// <summary>One snap step longer or shorter, landing on the snap, never below one step.</summary>
    private static double Stepped(double beats, int direction, double step)
    {
        double snapped = Math.Round(beats / step) * step;
        return Math.Max(step, snapped + direction * step);
    }
}
