using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Logic.Editor;
using Xanadu.Logic.Editor.History;

namespace Xanadu.Screens.Editor;

/// <summary>
/// The hold length stepper: - and + step the selected holds by one snap step
/// (one undo step). A hold just laid is selected, so it can be trimmed straight
/// away. The readout shows the first selected hold's length.
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
                continue;

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

        string text = _holds.Count > 0
            ? Label(EditorPlacementNote.HoldBeats(playhead.Tempo, _holds[0].GetComponent<Note>().TimeMs, _holds[0].GetComponent<Note>().Hold!.Value.TailMs))
            : "-";
        Query.EachEntity(new ShowLength(Query.Store, CommandBuffer, text));
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

    private readonly record struct ShowLength(EntityStore Store, CommandBuffer Buffer, string Text) : IEachEntity<EditorHoldLengthReadout>
    {
        public void Execute(ref EditorHoldLengthReadout _, int id) => UiText.Set(Store.GetEntityById(id), Text, Buffer);
    }
}
