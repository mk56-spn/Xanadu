using Xanadu.Ui.Systems.Canvas;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Drawers.Playfields;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Model.Editor;

namespace Xanadu.Drawers.Editor;

/// <summary>A faint copy of what a click would place, under the pointer.</summary>
public sealed class EditorGhostDrawer(EditorDrawSurface surface)
{
    private const float GhostAlpha = 0.45f;

    /// <summary>The note a click would place, in its key's colour, under the pointer.</summary>
    public void Draw(EditorFrame frame)
    {
        Entity editor = frame.Entity;
        if (!editor.TryGetComponent(out EditorPlacementGhost ghost) || !ghost.Visible)
            return;

        bool valid = EditorPlacementNote.CanPlace(editor, surface.Entities);
        PlayfieldView view = frame.View;
        Vector2 pointer = view.ScreenToPlayfield(ghost.Screen);
        Vector2 at = ViewportSnap.IsOn(surface.Entities) ? PlayfieldGrid.NearestCrossing(pointer, frame.Playfield) : pointer;
        long now = frame.Playhead.TimeMs;
        if (editor.GetComponent<EditorPlacement>().Mode == EditorPlacementMode.Draft)
        {
            Vector2 centre = view.PlayfieldToScreen(at);
            Vector2 half = new Vector2(Drafts.Diameter, Drafts.EmitterHeight) * view.Scale / 2f;
            surface.Canvas.DrawRect(surface.Target, new Rect2(centre - half, half * 2f), Faint(valid ? PlayfieldColors.DraftColor : Palette.Danger));
            return;
        }

        if (editor.GetComponent<EditorPlacement>().Mode is EditorPlacementMode.Pad or EditorPlacementMode.TimedPad)
        {
            Vector2 half = Pads.Size * view.Scale / 2f;
            Vector2 centre = view.PlayfieldToScreen(at);
            surface.Canvas.DrawRect(surface.Target, new Rect2(centre - half, half * 2f), Faint(valid ? PlayfieldColors.PadColor : Palette.Danger));
            return;
        }

        if (editor.GetComponent<EditorPlacement>().Mode == EditorPlacementMode.Chord)
        {
            DrawChordGhost(new Chord(now, at), view, valid);
            return;
        }

        // A note lands on its key's line of the chord in charge, wherever the click was; an entrance stays where clicked.
        Note note = EditorPlacementNote.Build(editor, now, at);
        if (note.IsOrb)
        {
            PlayfieldRenderer.DrawOrbGhost(surface.Canvas, surface.Target, view.PlayfieldToScreen(at), Orbs.Radius * view.Scale, valid);
            return;
        }

        if (note.IsEntrance)
        {
            PlayfieldRenderer.DrawEntranceMarker(surface.Canvas, surface.Target, note, view, frame.Playhead, selected: false);
            return;
        }

        Color color = valid ? PlayfieldColors.ColorFor(note) : Palette.Danger;
        Vector2 landing = Chords.Landing(surface.Entities, note);
        PlayfieldRenderer.DrawNoteShape(surface.Canvas, surface.Target, note, view.PlayfieldToScreen(landing), view.Scale, Faint(color));
    }

    private void DrawChordGhost(Chord chord, PlayfieldView view, bool valid)
    {
        for (int line = 0; line < Chords.LineCount; line++)
        {
            Color color = valid ? PlayfieldColors.ColorFor(Xanadu.Model.Settings.KeybindActions.ActionKeys[line]) : Palette.Danger;
            PlayfieldRenderer.DrawFadedLine(surface.Canvas, surface.Target, view.PlayfieldToScreen(Chords.Top(chord, line)), view.PlayfieldToScreen(Chords.Landing(chord, line)), Faint(color), 3f);
        }
    }

    private static Color Faint(Color color) => new(color.R, color.G, color.B, color.A * GhostAlpha);
}
