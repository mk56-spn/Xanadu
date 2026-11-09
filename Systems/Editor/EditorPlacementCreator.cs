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
        if (EditorItems.WouldOverfill(scene.Store, editor))
            return;

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
            EditorItems.Place(scene.Store, new StartPoint(
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
            Entity text = EditorItems.Place(scene.Store, new LevelText(textPosition, LevelText.Placeholder));
            history.Record(textBefore, history.Capture());
            editor.GetComponent<NoteSelection>().Note = text;
            return;
        }

        if (editor.HasComponent<EditorPlacement>() &&
            editor.GetComponent<EditorPlacement>().Mode ==
            EditorPlacementMode.ScreenRead)
        {
            PlayfieldView regionView = editor.GetComponent<PlayfieldView>();
            Playfield regionField = editor.GetComponent<Playfield>();
            Vector2 centre = regionView.ScreenToPlayfield(screenPosition);
            if (!EditorItemPicker.IsInside(centre, regionField))
                return;

            ScreenReadRegion region = ScreenReadRegion.Centred(
                centre,
                ScreenReadRegion.StartSize(regionView.ScreenSize / regionView.Scale));
            region.Position = GridPlacement.Settle(
                region.Position,
                region.Size,
                regionField,
                ViewportSnap.IsOn(scene.Store));

            // A region starts where the user is working: the editor's current
            // layer, in front of what is already there (step 2's default table).
            EditorLayers layers = EditorLayers.Of(editor);
            int on = layers.Current == 0 ? ItemLayer.Gameplay : layers.Current;
            EditorSnapshot regionBefore = history.Capture();
            Entity created = EditorItems.Create(scene.Store, region);
            ItemLayer.Put(created, new ItemLayer(on, 0));
            ItemLayer.OnTop(scene.Store, created);
            history.Record(regionBefore, history.Capture());
            editor.GetComponent<NoteSelection>().Note = created;
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

            // The click is where the blades stand; with snap on, the base and left
            // side sit on grid lines, the same lines block edges sit on.
            GrassPatch patch = editor.GetComponent<EditorPlacement>().Grass;
            patch.Position = GridPlacement.PlaceGrass(
                grassPosition,
                patch,
                editor.GetComponent<Playfield>(),
                ViewportSnap.IsOn(scene.Store));
            patch.Seed = GrassPatch.NewSeed();
            EditorSnapshot grassBefore = history.Capture();
            Entity grass = EditorItems.Place(scene.Store, patch);
            history.Record(grassBefore, history.Capture());
            editor.GetComponent<NoteSelection>().Note = grass;
            return;
        }

        if (editor.HasComponent<EditorPlacement>() &&
            editor.GetComponent<EditorPlacement>().Mode ==
            EditorPlacementMode.Flare)
        {
            PlayfieldView flareView = editor.GetComponent<PlayfieldView>();
            Vector2 flarePosition = flareView.ScreenToPlayfield(screenPosition);
            if (!EditorItemPicker.IsInside(flarePosition, editor.GetComponent<Playfield>()))
                return;

            // The click is where the core goes; with snap on, it sits on the half-cell grid.
            LevelFlare flare = editor.GetComponent<EditorPlacement>().Flare;
            flare.Position = GridPlacement.PlaceFlare(
                flarePosition,
                flare,
                editor.GetComponent<Playfield>(),
                ViewportSnap.IsOn(scene.Store));
            EditorSnapshot flareBefore = history.Capture();
            Entity placed = EditorItems.Place(scene.Store, flare);
            history.Record(flareBefore, history.Capture());
            editor.GetComponent<NoteSelection>().Note = placed;
            return;
        }

        if (editor.HasComponent<EditorPlacement>() &&
            editor.GetComponent<EditorPlacement>().Mode ==
            EditorPlacementMode.FrictionBar)
        {
            PlayfieldView barView = editor.GetComponent<PlayfieldView>();
            Playfield barField = editor.GetComponent<Playfield>();
            Vector2 barPosition = barView.ScreenToPlayfield(screenPosition);
            if (!EditorItemPicker.IsInside(barPosition, barField))
                return;

            float height = GridPlacement.NewFrictionBarHeight(barField);
            var bar = new FrictionBar(
                GridPlacement.PlaceFrictionBar(barPosition, height, barField, ViewportSnap.IsOn(scene.Store)),
                height);
            EditorSnapshot barBefore = history.Capture();
            Entity placedBar = EditorItems.Place(scene.Store, bar);
            history.Record(barBefore, history.Capture());
            editor.GetComponent<NoteSelection>().Note = placedBar;
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
            EditorItems.Place(scene.Store, new Mine(
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

        if (editor.GetComponent<LevelInfo>().Song is null)
        {
            EditorWarningPopup.Request(editor, EditorWarningPopup.NoSongMessage);
            return;
        }

        EditorSnapshot before = history.Capture();
        long time = EditorTiming.Snap(playhead.TimeMs, playhead);
        if (ViewportSnap.IsOn(scene.Store))
            playfieldPosition = PlayfieldGrid.NearestCrossing(playfieldPosition, playfield);
        Note note = EditorPlacementNote.Build(editor, time, playfieldPosition);
        Entity created = EditorItems.Place(scene.Store, note);
        history.Record(before, history.Capture());
        editor.GetComponent<NoteSelection>().Note = created;
        EditorAutoAdvance.AfterPlacing(scene.Store, editor, time);
    }
}
