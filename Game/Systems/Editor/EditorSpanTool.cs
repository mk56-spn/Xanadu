using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.History;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Lays route notes. The first click puts the head down where clicked, at the
/// snapped playhead, running at the speed picked then. Each click after, with
/// the playhead moved on, adds a turn at the snapped playhead; where it is
/// clicked does not matter, since the player picks the way. Enter finishes it
/// (the last time is the route's end) as one undo step; Esc, or picking another tool, drops it.
/// </summary>
public sealed class EditorRouteTool(EditorSceneQuery scene, EditorHistory history)
{
    /// <summary>Whether a route is being laid.</summary>
    public bool Drafting(Entity editor) =>
        !editor.IsNull && editor.TryGetComponent(out EditorRouteDraft draft) && draft.Active;

    /// <summary>Acts on Enter and Esc.</summary>
    public void Update(Entity editor)
    {
        if (editor.IsNull || !editor.HasComponent<EditorRouteDraft>())
            return;

        ref EditorRouteDraft draft = ref editor.GetComponent<EditorRouteDraft>();
        if (draft.CancelRequested)
            Clear(ref draft);
        else if (draft.FinishRequested)
            Finish(editor, ref draft);

        draft.CancelRequested = false;
        draft.FinishRequested = false;
    }

    /// <summary>Puts the head down, or adds a turn at the playhead; true when the press was the tool's.</summary>
    public bool TryDraft(Entity editor, Vector2 screenPosition, PlayfieldView view, Playfield playfield)
    {
        if (!editor.HasComponent<EditorRouteDraft>())
            return false;

        ref EditorRouteDraft draft = ref editor.GetComponent<EditorRouteDraft>();
        Playhead playhead = editor.GetComponent<Playhead>();
        if (draft.Active)
        {
            long after = EditorTiming.Snap(playhead.TimeMs, playhead) - draft.StartMs;
            long last = draft.TimesMs.Count > 0 ? draft.TimesMs[^1] : 0;
            if (after > last && draft.TimesMs.Count < NoteRoute.MaxTimes)
                draft.TimesMs.Add(after);
            return true;
        }

        Vector2 point = view.ScreenToPlayfield(screenPosition);
        if (!PlayfieldGrid.IsInside(point, playfield))
            return false;

        if (editor.GetComponent<LevelInfo>().Song is null)
        {
            EditorWarningPopup.Request(editor, EditorWarningPopup.NoSongMessage);
            return true;
        }

        if (ViewportSnap.IsOn(scene.Store))
            point = PlayfieldGrid.NearestCrossing(point, playfield);
        draft.Head = point;
        draft.StartMs = EditorTiming.Snap(playhead.TimeMs, playhead);
        draft.Strength = editor.GetComponent<EditorPlacement>().Strength;
        draft.TimesMs.Clear();
        draft.Active = true;
        return true;
    }

    /// <summary>Turns the draft into a selected route note when it has an end; one without is dropped.</summary>
    private void Finish(Entity editor, ref EditorRouteDraft draft)
    {
        if (draft.Active && draft.TimesMs.Count >= NoteRoute.MinTimes && !EditorItems.WouldOverfill(scene.Store, editor))
        {
            Note head = EditorPlacementNote.Build(editor, draft.StartMs, draft.Head) with
            {
                Route = new NoteRoute(draft.Strength, draft.TimesMs.ToArray()),
            };
            EditorSnapshot before = history.Capture();
            Entity created = EditorItems.Place(scene.Store, head);
            history.Record(before, history.Capture());
            editor.GetComponent<NoteSelection>().Note = created;
        }

        Clear(ref draft);
    }

    private static void Clear(ref EditorRouteDraft draft)
    {
        draft.TimesMs.Clear();
        draft.Active = false;
    }
}
