using System;
using Xanadu.Drawers.Playfields;

namespace Xanadu.Drawers.Editor;

/// <summary>The editor's playfield items, drawn the way play draws them.</summary>
public sealed class EditorItemsDrawer(EditorDrawSurface surface)
{
    /// <summary>
    /// Draws every item, with <paramref name="drawWedge"/> drawing what stands
    /// in for the player.
    /// </summary>
    public void Draw(EditorFrame frame, Action? drawWedge) =>
        PlayfieldRenderer.DrawItems(
            surface.Canvas,
            surface.Target,
            surface.Entities,
            new PlayfieldItemFrame(
                frame.View,
                frame.Playfield,
                frame.Playhead,
                frame.Seconds,
                Editor: true,
                frame.Selection.Contains,
                frame.Selection.Note),
            drawWedge);
}
