using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Model.Ecs.Components;
using Xanadu.Editor;
using Xanadu.Editor.History;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Model.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The point-by-point tools, polygon and curve, sharing one draft. Polygon:
/// each click lays a point of the outline, and clicking the first point again,
/// or Enter, closes it into a shape. Curve: each click lays a point the path
/// runs through, at the 1×/2×/3× speed picked when it was clicked, starting
/// at the playhead; clicking the last point again, or Enter, finishes it into
/// an action note (the head, for the key picked) carrying that path.
/// Either way the result is one undo step. Esc, or picking another tool,
/// drops the draft.
/// </summary>
public sealed class EditorPolygonTool(
    EditorSceneQuery scene,
    EditorItemPicker picker,
    EditorHistory history)
{
    /// <summary>Acts on Enter and Esc, and drops the draft when its tool is put away.</summary>
    public void Update(Entity editor)
    {
        if (editor.IsNull || !editor.HasComponent<EditorPolygonDraft>())
            return;

        ref EditorPolygonDraft draft = ref editor.GetComponent<EditorPolygonDraft>();
        EditorPlacementMode? mode = DraftMode(editor);
        if (draft.CancelRequested || mode is null || (draft.Active && mode != draft.Mode))
            Clear(ref draft);
        else if (draft.FinishRequested)
            Finish(editor, ref draft);

        draft.CancelRequested = false;
        draft.FinishRequested = false;
    }

    /// <summary>Lays or finishes the draft when the polygon or curve tool is out; true when the press was the tool's.</summary>
    public bool TryDraft(
        Entity editor,
        Vector2 screenPosition,
        PlayfieldView view,
        Playfield playfield)
    {
        if (!editor.HasComponent<EditorPolygonDraft>() || DraftMode(editor) is not { } mode)
            return false;

        ref EditorPolygonDraft draft = ref editor.GetComponent<EditorPolygonDraft>();
        if (draft.Active && draft.Mode != mode)
            Clear(ref draft);
        draft.Mode = mode;

        if (mode == EditorPlacementMode.Polygon &&
            draft.Points.Count >= 3 &&
            view.PlayfieldToScreen(draft.Points[0]).DistanceTo(screenPosition) <= EditorPolygonDraft.CloseRadius)
        {
            Finish(editor, ref draft);
            return true;
        }

        if (mode == EditorPlacementMode.Curve &&
            draft.Points.Count >= NotePath.MinPoints &&
            view.PlayfieldToScreen(draft.Points[^1]).DistanceTo(screenPosition) <= EditorPolygonDraft.CloseRadius)
        {
            Finish(editor, ref draft);
            return true;
        }

        Vector2 point = view.ScreenToPlayfield(screenPosition);
        if (!PlayfieldGrid.IsInside(point, playfield))
            return true;

        if (mode == EditorPlacementMode.Curve && draft.Points.Count == 0)
        {
            if (editor.GetComponent<LevelInfo>().Song is null)
            {
                EditorWarningPopup.Request(editor, EditorWarningPopup.NoSongMessage);
                return true;
            }

            Playhead playhead = editor.GetComponent<Playhead>();
            draft.StartMs = EditorTiming.Snap(playhead.TimeMs, playhead);
        }

        if (ViewportSnap.IsOn(scene.Store))
            point = PlayfieldGrid.NearestCrossing(point, playfield);
        if (mode == EditorPlacementMode.Curve && draft.Points.Count >= NotePath.MaxPoints)
            return true;
        if (draft.Points.Count == 0 || draft.Points[^1] != point)
        {
            draft.Points.Add(point);
            draft.Speeds.Add(EditorPlacementNote.MomentumFor(editor.GetComponent<EditorPlacement>().Strength));
        }
        return true;
    }

    /// <summary>The draft tool that is out, or null when it is neither.</summary>
    private static EditorPlacementMode? DraftMode(Entity editor)
    {
        if (!editor.TryGetComponent(out EditorPlacement placement))
            return null;
        return placement.Mode is EditorPlacementMode.Polygon or EditorPlacementMode.Curve ? placement.Mode : null;
    }

    /// <summary>Turns the draft into a selected item when it has enough points; a shorter one is dropped.</summary>
    private void Finish(Entity editor, ref EditorPolygonDraft draft)
    {
        if (draft.Mode == EditorPlacementMode.Curve)
            FinishCurve(editor, ref draft);
        else
            FinishPolygon(editor, ref draft);
        Clear(ref draft);
    }

    private void FinishPolygon(Entity editor, ref EditorPolygonDraft draft)
    {
        if (draft.Points.Count < 3 || EditorItems.WouldOverfill(scene.Store, editor))
            return;

        EditorSnapshot before = history.Capture();
        Entity created = EditorItems.Place(
            scene.Store,
            LevelPolygon.FromOutline(draft.Points, PlayfieldColors.PolygonFill));
        history.Record(before, history.Capture());
        editor.GetComponent<NoteSelection>().Note = created;
    }

    private void FinishCurve(Entity editor, ref EditorPolygonDraft draft)
    {
        if (draft.Points.Count < NotePath.MinPoints || EditorItems.WouldOverfill(scene.Store, editor))
            return;

        // A curve is an action note at its first point, for the key picked, carrying the path.
        Note head = EditorPlacementNote.Build(editor, draft.StartMs, draft.Points[0]) with
        {
            Path = new NotePath(draft.Points.ToArray(), draft.Speeds.ToArray()),
        };
        EditorSnapshot before = history.Capture();
        Entity created = EditorItems.Place(scene.Store, head);
        history.Record(before, history.Capture());
        editor.GetComponent<NoteSelection>().Note = created;
    }

    private static void Clear(ref EditorPolygonDraft draft)
    {
        draft.Points.Clear();
        draft.Speeds.Clear();
    }
}
