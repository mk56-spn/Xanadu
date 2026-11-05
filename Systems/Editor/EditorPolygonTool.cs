using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The polygon tool: each click lays a point of the outline, and clicking the
/// first point again, or Enter, closes it into a shape as one undo step. Esc,
/// or picking another tool, drops the outline.
/// </summary>
public sealed class EditorPolygonTool(
    EditorSceneQuery scene,
    EditorItemPicker picker,
    EditorHistory history)
{
    /// <summary>A click this close to the first point, in screen pixels, closes the outline.</summary>
    public const float CloseRadius = 12f;

    /// <summary>Acts on Enter and Esc, and drops the outline when the tool is put away.</summary>
    public void Update(Entity editor)
    {
        if (editor.IsNull || !editor.HasComponent<EditorPolygonDraft>())
            return;

        ref EditorPolygonDraft draft = ref editor.GetComponent<EditorPolygonDraft>();
        bool tool = editor.HasComponent<EditorPlacement>() &&
                    editor.GetComponent<EditorPlacement>().Mode == EditorPlacementMode.Polygon;
        if (draft.CancelRequested || !tool)
            draft.Points.Clear();
        else if (draft.FinishRequested)
            Finish(editor, ref draft);

        draft.CancelRequested = false;
        draft.FinishRequested = false;
    }

    /// <summary>Lays or closes the outline when the polygon tool is out; true when the press was the tool's.</summary>
    public bool TryDraft(
        Entity editor,
        Vector2 screenPosition,
        PlayfieldView view,
        Playfield playfield)
    {
        if (!editor.HasComponent<EditorPolygonDraft>() ||
            !editor.HasComponent<EditorPlacement>() ||
            editor.GetComponent<EditorPlacement>().Mode != EditorPlacementMode.Polygon)
        {
            return false;
        }

        ref EditorPolygonDraft draft = ref editor.GetComponent<EditorPolygonDraft>();
        if (draft.Points.Count >= 3 &&
            view.PlayfieldToScreen(draft.Points[0]).DistanceTo(screenPosition) <= CloseRadius)
        {
            Finish(editor, ref draft);
            return true;
        }

        Vector2 point = view.ScreenToPlayfield(screenPosition);
        if (!EditorItemPicker.IsInside(point, playfield))
            return true;

        if (ViewportSnap.IsOn(scene.Store))
            point = PlayfieldGrid.NearestCrossing(point, playfield);
        if (draft.Points.Count == 0 || draft.Points[^1] != point)
            draft.Points.Add(point);
        return true;
    }

    /// <summary>Turns an outline of three or more points into a selected shape; a shorter one is dropped.</summary>
    private void Finish(Entity editor, ref EditorPolygonDraft draft)
    {
        if (draft.Points.Count >= 3)
        {
            EditorSnapshot before = history.Capture();
            Entity created = EditorItems.Place(
                scene.Store,
                LevelPolygon.FromOutline(draft.Points, PlayfieldRenderer.PolygonFill));
            history.Record(before, history.Capture());
            editor.GetComponent<NoteSelection>().Note = created;
        }

        draft.Points.Clear();
    }
}
