using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Lays whatever the current tool places on a click: start points, text, grass,
/// mines or a timed note. Each placement is one undo step, and the new item is
/// selected so its settings show.
/// </summary>
public sealed class EditorPlacementCreator(EditorSceneQuery scene, EditorHistory history)
{
    public void Create(Entity editor, Vector2 screenPosition)
    {
        if (editor.HasComponent<EditorPlacement>() &&
            editor.GetComponent<EditorPlacement>().Mode ==
            EditorPlacementMode.Start)
        {
            PlayfieldView startView = editor.GetComponent<PlayfieldView>();
            Playfield startField = editor.GetComponent<Playfield>();
            Vector2 startPosition = startView.ScreenToPlayfield(screenPosition);
            if (!EditorItemPicker.IsInside(startPosition, startField))
                return;

            Playhead startPlayhead = editor.GetComponent<Playhead>();
            EditorSnapshot startBefore = history.Capture();
            EditorItems.Create(scene.Store, new StartPoint(
                "",
                EditorTiming.Snap(startPlayhead.TimeMs, startPlayhead),
                startPosition,
                System.Numerics.Vector2.Zero,
                new Vector2(0, -1)));
            history.Record(startBefore, history.Capture());
            return;
        }

        if (editor.HasComponent<EditorPlacement>() &&
            editor.GetComponent<EditorPlacement>().Mode ==
            EditorPlacementMode.Text)
        {
            PlayfieldView textView = editor.GetComponent<PlayfieldView>();
            Vector2 textPosition = textView.ScreenToPlayfield(screenPosition);
            if (!EditorItemPicker.IsInside(textPosition, editor.GetComponent<Playfield>()))
                return;

            EditorSnapshot textBefore = history.Capture();
            Entity text = EditorItems.Create(scene.Store, new LevelText(textPosition, LevelText.Placeholder));
            history.Record(textBefore, history.Capture());
            editor.GetComponent<NoteSelection>().Note = text;
            return;
        }

        if (editor.HasComponent<EditorPlacement>() &&
            editor.GetComponent<EditorPlacement>().Mode ==
            EditorPlacementMode.Grass)
        {
            PlayfieldView grassView = editor.GetComponent<PlayfieldView>();
            Vector2 grassPosition = grassView.ScreenToPlayfield(screenPosition);
            if (!EditorItemPicker.IsInside(grassPosition, editor.GetComponent<Playfield>()))
                return;

            // The click is where the blades stand; with snap on, the base sits on a grid line.
            if (ViewportSnap.IsOn(scene.Store))
                grassPosition = PlayfieldGrid.NearestCrossing(grassPosition, editor.GetComponent<Playfield>());
            GrassPatch patch = editor.GetComponent<EditorPlacement>().Grass;
            patch.Position = grassPosition;
            patch.Seed = GrassPatch.NewSeed();
            EditorSnapshot grassBefore = history.Capture();
            Entity grass = EditorItems.Create(scene.Store, patch);
            history.Record(grassBefore, history.Capture());
            editor.GetComponent<NoteSelection>().Note = grass;
            return;
        }

        if (editor.HasComponent<EditorPlacement>() &&
            editor.GetComponent<EditorPlacement>().Mode ==
            EditorPlacementMode.Mines)
        {
            PlayfieldView view = editor.GetComponent<PlayfieldView>();
            Playfield playfield = editor.GetComponent<Playfield>();
            Vector2 position = view.ScreenToPlayfield(screenPosition);
            if (!EditorItemPicker.IsInside(position, playfield))
                return;

            Playhead playhead = editor.GetComponent<Playhead>();
            EditorSnapshot before = history.Capture();
            EditorItems.Create(scene.Store, new Mine(
                EditorTiming.Snap(playhead.TimeMs, playhead),
                position));
            history.Record(before, history.Capture());
            return;
        }

        CreateNoteAt(editor, screenPosition);
    }

    private void CreateNoteAt(Entity editor, Vector2 screenPosition)
    {
        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        Playhead playhead = editor.GetComponent<Playhead>();
        Vector2 playfieldPosition = view.ScreenToPlayfield(screenPosition);
        if (!EditorItemPicker.IsInside(playfieldPosition, playfield))
            return;

        if (string.IsNullOrEmpty(editor.GetComponent<LevelInfo>().AudioPath))
        {
            EditorWarningPopup.Request(editor, EditorWarningPopup.NoSongMessage);
            return;
        }

        EditorSnapshot before = history.Capture();
        long time = EditorTiming.Snap(playhead.TimeMs, playhead);
        if (EditorPlacementNote.NeedsShape(editor))
            return;

        if (ViewportSnap.IsOn(scene.Store))
            playfieldPosition = PlayfieldGrid.NearestCrossing(playfieldPosition, playfield);
        Note note = EditorPlacementNote.Build(editor, time, playfieldPosition);
        bool isFormStep = note.Kind == NoteKind.ConstructionStep;
        if (isFormStep)
            note.ShapePiece = NextShapePiece(note.ShapeId);

        Entity created = EditorItems.Create(scene.Store, note);
        if (isFormStep)
            ReindexConstructionSteps(note.ShapeId);
        history.Record(before, history.Capture());
        editor.GetComponent<NoteSelection>().Note = created;
        EditorAutoAdvance.AfterPlacing(scene.Store, editor, time);
    }

    private int NextShapePiece(int shapeId)
    {
        int count = 0;
        foreach (Entity entity in scene.Store.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (note.Kind == NoteKind.ConstructionStep &&
                note.ShapeId == shapeId)
            {
                count++;
            }
        }

        return count;
    }

    private void ReindexConstructionSteps(int shapeId)
    {
        var steps = new List<Entity>();
        foreach (Entity entity in scene.Store.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (note.Kind == NoteKind.ConstructionStep &&
                note.ShapeId == shapeId)
            {
                steps.Add(entity);
            }
        }

        steps.Sort((a, b) =>
            a.GetComponent<Note>().TimeMs.CompareTo(
                b.GetComponent<Note>().TimeMs));
        for (int i = 0; i < steps.Count; i++)
        {
            ref Note note = ref steps[i].GetComponent<Note>();
            note.ShapePiece = i;
        }
    }
}
