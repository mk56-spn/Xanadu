using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Logic.Editor.History;

namespace Xanadu.Logic.Editor;

/// <summary>
/// Lays the notes that span time, route and hold, the same way, after osu!'s
/// editor (spinner and slider placement): a left click on the field puts the head
/// there at the snapped playhead, and the end follows the playhead live from then
/// on. A left click marks a time: for a route a turn (where it is clicked does not
/// matter, the player picks the way) and more can follow; for a hold its tail,
/// which sits where clicked, and the hold is done. Right click or Enter ends either
/// at the playhead (a hold's tail where the pointer is); picking another tool ends
/// it too; Esc drops it. Never shorter than one snap step. One undo step each.
/// </summary>
public sealed class EditorSpanTool(EditorSceneQuery scene, EditorHistory history)
{
    /// <summary>Whether the tool places by clicking a head and then times.</summary>
    public static bool Lays(EditorPlacementMode mode) =>
        mode is EditorPlacementMode.Curve or EditorPlacementMode.Hold;

    /// <summary>The note the last finish placed, so the input system can treat it as just placed.</summary>
    public Entity Placed { get; set; }

    /// <summary>Whether a route or hold is being laid.</summary>
    public bool Drafting(Entity editor) =>
        !editor.IsNull && editor.TryGetComponent(out EditorSpanDraft draft) && draft.Active;

    /// <summary>
    /// Acts on Enter and Esc, and ends the draft when the tool changes.
    /// <paramref name="pointer"/> is where a hold's tail goes, in playfield units, when the pointer is over the field.
    /// </summary>
    public void Update(Entity editor, Vector2? pointer)
    {
        if (editor.IsNull || !editor.HasComponent<EditorSpanDraft>())
            return;

        ref EditorSpanDraft draft = ref editor.GetComponent<EditorSpanDraft>();
        if (draft.CancelRequested)
            Clear(ref draft);
        else if (draft.Active && (draft.FinishRequested || editor.GetComponent<EditorPlacement>().Mode != draft.Mode))
            FinishAtPlayhead(editor, ref draft, pointer);

        draft.CancelRequested = false;
        draft.FinishRequested = false;
    }

    /// <summary>Right click: ends the draft at the playhead, a hold's tail at the click.</summary>
    public void Finish(Entity editor, Vector2 screenPosition, PlayfieldView view, Playfield playfield)
    {
        if (!Drafting(editor))
            return;
        FinishAtPlayhead(editor, ref editor.GetComponent<EditorSpanDraft>(), FieldPoint(screenPosition, view, playfield));
    }

    /// <summary>Left click: puts the head down, or marks a time at the playhead; true when the press was the tool's.</summary>
    public bool TryDraft(Entity editor, Vector2 screenPosition, PlayfieldView view, Playfield playfield)
    {
        if (!editor.HasComponent<EditorSpanDraft>())
            return false;

        ref EditorSpanDraft draft = ref editor.GetComponent<EditorSpanDraft>();
        Vector2 point = FieldPoint(screenPosition, view, playfield);
        Playhead playhead = editor.GetComponent<Playhead>();

        if (draft.Active)
        {
            long at = EditorPlacementNote.SpanEndMs(playhead, draft);
            if (draft.Mode == EditorPlacementMode.Hold)
                Commit(editor, ref draft, at, point - draft.Head);
            else
                AddTurn(ref draft, at);
            return true;
        }

        if (!PlayfieldGrid.IsInside(view.ScreenToPlayfield(screenPosition), playfield))
            return false;

        if (editor.GetComponent<LevelInfo>().Song is null)
        {
            EditorWarnings.Request(editor, EditorWarnings.NoSongMessage);
            return true;
        }

        EditorPlacement placement = editor.GetComponent<EditorPlacement>();
        draft.Mode = placement.Mode;
        draft.Head = point;
        draft.StartMs = EditorTiming.Snap(playhead.TimeMs, playhead);
        draft.Strength = placement.Strength;
        draft.TimesMs.Clear();
        draft.Active = true;
        return true;
    }

    /// <summary>The playfield point under the pointer, on the grid when snap is on, kept inside the field.</summary>
    private Vector2 FieldPoint(Vector2 screenPosition, PlayfieldView view, Playfield playfield)
    {
        Vector2 point = view.ScreenToPlayfield(screenPosition);
        point = new Vector2(System.Math.Clamp(point.X, 0, playfield.Width), System.Math.Clamp(point.Y, 0, playfield.Height));
        return ViewportSnap.IsOn(scene.Store) ? PlayfieldGrid.NearestCrossing(point, playfield) : point;
    }

    private void FinishAtPlayhead(Entity editor, ref EditorSpanDraft draft, Vector2? tailAt)
    {
        long at = EditorPlacementNote.SpanEndMs(editor.GetComponent<Playhead>(), draft);
        if (draft.Mode == EditorPlacementMode.Hold)
        {
            Commit(editor, ref draft, at, tailAt is { } spot ? spot - draft.Head : EditorPlacementNote.DefaultHoldTail);
            return;
        }

        AddTurn(ref draft, at);
        Commit(editor, ref draft, 0, default);
    }

    private static void AddTurn(ref EditorSpanDraft draft, long at)
    {
        long last = draft.TimesMs.Count > 0 ? draft.TimesMs[^1] : 0;
        if (at > last && draft.TimesMs.Count < NoteRoute.MaxTimes)
            draft.TimesMs.Add(at);
    }

    /// <summary>
    /// Turns the draft into a selected note: a route with its turns, or a hold
    /// with its tail <paramref name="tailMs"/> after the head at <paramref name="tail"/> from it.
    /// </summary>
    private void Commit(Entity editor, ref EditorSpanDraft draft, long tailMs, Vector2 tail)
    {
        Note? built = draft.Mode == EditorPlacementMode.Hold
            ? EditorPlacementNote.BuildHold(editor, draft.StartMs, draft.Head, tailMs, tail)
            : draft.TimesMs.Count >= NoteRoute.MinTimes
                ? EditorPlacementNote.Build(editor, draft.StartMs, draft.Head) with { Route = new NoteRoute(draft.Strength, draft.TimesMs.ToArray()) }
                : null;

        if (built is { } note && !EditorItems.WouldOverfill(scene.Store, editor))
        {
            EditorSnapshot before = history.Capture();
            Entity created = EditorItems.Place(scene.Store, note);
            history.Record(before, history.Capture());
            editor.GetComponent<NoteSelection>().Note = created;
            Placed = created;
        }

        Clear(ref draft);
    }

    private static void Clear(ref EditorSpanDraft draft)
    {
        draft.TimesMs.Clear();
        draft.Active = false;
    }
}
