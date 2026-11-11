using System;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;
using Xanadu.Systems.Shaders;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The editor's playfield items, drawn the way play draws them: layer by layer,
/// back to front, with the player wedge between the layers behind it and those
/// in front (Docs/render-architecture.md §3).
/// </summary>
public sealed class EditorItemsDrawer(EditorDrawSurface surface, IPassSurfaces? passes = null)
{
    /// <summary>
    /// Draws every item, with <paramref name="drawWedge"/> drawing what stands
    /// in for the player in the wedge.
    /// </summary>
    public void Draw(EditorFrame frame, Action? drawWedge)
    {
        Func<Entity, bool> isSelected = frame.Selection.Contains;
        EditorLayers layers = EditorLayers.Of(frame.Entity);
        PlayfieldRenderer.DrawLayers(
            surface.Canvas,
            surface.Target,
            surface.Entities,
            new PlayfieldItemFrame(
                frame.View,
                frame.Playfield,
                frame.Playhead,
                frame.Seconds,
                Editor: true,
                isSelected,
                frame.Selection.Note,
                layers.Shows,
                frame.Entity.TryGetComponent(out LevelGroups groups) ? groups.Groups : null,
                frame.Entity.TryGetComponent(out LevelLayers table) ? table.Layers : null,
                passes),
            drawWedge);
    }
}
