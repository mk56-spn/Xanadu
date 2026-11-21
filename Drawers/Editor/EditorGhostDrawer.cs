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
        Note note = EditorPlacementNote.Build(editor, frame.Playhead.TimeMs, at);
        Color color = valid ? PlayfieldColors.ColorFor(note) : Palette.Danger;
        PlayfieldRenderer.DrawNoteBody(
            surface.Canvas,
            surface.Target,
            note,
            view.PlayfieldToScreen(at),
            PlayfieldMetrics.NoteSize * view.Scale / 2f,
            new Color(color.R, color.G, color.B, color.A * GhostAlpha));
    }
}
