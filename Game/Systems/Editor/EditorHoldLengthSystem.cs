using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Editor;
using Xanadu.Editor.History;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The hold length stepper: - and + step by one snap step. With holds
/// selected they step those (one undo step); otherwise the length new holds
/// get. The readout shows the first selected hold's length, else the tool's.
/// </summary>
public sealed class EditorHoldLengthSystem(EntityStore entities, EditorHistory history) : QuerySystem<EditorHoldLengthReadout>
{
    private readonly ButtonPress _presses = new();
    private readonly List<Entity> _holds = new();

    protected override void OnUpdate()
    {
        Entity editor = EditorSceneQuery.First<EditorPlacement, Playhead>(entities);
        if (editor.IsNull)
            return;

        Playhead playhead = editor.GetComponent<Playhead>();
        double step = EditorTiming.StepBeats(playhead);
        SelectedHolds(editor);

        foreach (Entity button in EditorButton.All(entities, EditorButtonKind.HoldLength))
        {
            if (!_presses.Started(button))
                continue;

            int arg = button.GetComponent<UiCommand>().Arg;
            if (_holds.Count == 0)
            {
                ref EditorPlacement placement = ref editor.GetComponent<EditorPlacement>();
                placement.HoldBeats = Stepped(placement.HoldBeats, arg, step);
                continue;
            }

            EditorSnapshot before = history.Capture();
            foreach (Entity hold in _holds)
            {
                ref Note note = ref hold.GetComponent<Note>();
                NoteHold tail = note.Hold!.Value;
                double beats = Stepped(EditorPlacementNote.HoldBeats(playhead.Tempo, note.TimeMs, tail.TailMs), arg, step);
                note.Hold = tail with { TailMs = EditorPlacementNote.HoldTailMs(playhead.Tempo, note.TimeMs, beats) };
            }

            history.Record(before, history.Capture());
        }

        double shown = _holds.Count > 0
            ? EditorPlacementNote.HoldBeats(playhead.Tempo, _holds[0].GetComponent<Note>().TimeMs, _holds[0].GetComponent<Note>().Hold!.Value.TailMs)
            : editor.GetComponent<EditorPlacement>().HoldBeats;
        string text = Label(shown);
        CommandBuffer buffer = CommandBuffer;
        Query.ForEachEntity((ref _, entity) => UiText.Set(entity, text, buffer));
    }

    private void SelectedHolds(Entity editor)
    {
        _holds.Clear();
        if (!editor.TryGetComponent(out NoteSelection selection))
            return;
        foreach (Entity entity in selection.AllNotes)
        {
            if (!entity.IsNull && entity.TryGetComponent(out Note note) && note.IsHold)
                _holds.Add(entity);
        }
    }

    /// <summary>One snap step longer or shorter, landing on the snap, never below one step.</summary>
    private static double Stepped(double beats, int direction, double step)
    {
        double snapped = Math.Round(beats / step) * step;
        return Math.Max(step, snapped + direction * step);
    }

    private static string Label(double beats)
    {
        double rounded = Math.Round(beats, 2);
        return rounded == 1d ? "1 BEAT" : $"{rounded:0.##} BEATS";
    }
}
