using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>A faint copy of what a click would place, under the pointer.</summary>
public sealed class EditorGhostDrawer(EditorDrawSurface surface)
{
    private const float GhostAlpha = 0.45f;

    public void Draw(EditorFrame frame)
    {
        DrawPolygonDraft(frame.Entity, frame.View);
        if (!frame.Entity.TryGetComponent(out EditorPlacementGhost ghost) ||
            !ghost.Visible ||
            !frame.Entity.TryGetComponent(out EditorPlacement placement))
        {
            return;
        }

        PlayfieldView view = frame.View;
        Playfield playfield = frame.Playfield;
        Entity editor = frame.Entity;
        Vector2 pointer = view.ScreenToPlayfield(ghost.Screen);
        float degrees = placement.Degrees;
        bool snap = ViewportSnap.IsOn(surface.Entities);
        switch (placement.Mode)
        {
            case EditorPlacementMode.Blocks:
                Vector2 size = GridPlacement.NewBlockSize(playfield, degrees, placement.BlockSize);
                Vector2 corner = GridPlacement.Place(pointer, size, playfield, snap);
                var block = new Rect2(view.PlayfieldToScreen(corner), size * view.Scale);
                surface.Canvas.DrawRect(surface.Target, block, Faint(PlayfieldRenderer.BlockFill));
                surface.Canvas.DrawBorder(
                    surface.Target,
                    block,
                    Faint(Block.DefaultOutlineColor),
                    Block.DefaultOutlineWidth * view.Scale);
                break;

            case EditorPlacementMode.Spikes:
                Vector2 tile = GridPlacement.NewSpikeSize(playfield);
                SpikeTriangle triangle = SpikeRules.ScreenTriangle(
                    new Spike(GridPlacement.Place(pointer, tile, playfield, snap), tile, degrees),
                    view);
                PlayfieldRenderer.DrawSpikeBody(
                    surface.Canvas,
                    surface.Target,
                    triangle,
                    Block.DefaultOutlineWidth * view.Scale,
                    Faint(PlayfieldRenderer.SpikeColor),
                    Faint(Block.DefaultOutlineColor));
                break;

            case EditorPlacementMode.Mines:
                surface.Canvas.DrawCircle(
                    surface.Target,
                    ghost.Screen,
                    PlayfieldRenderer.MineSize * view.Scale / 2f,
                    Faint(Palette.DangerBright));
                break;

            case EditorPlacementMode.Text:
                var text = new LevelText(pointer, LevelText.Placeholder);
                surface.Canvas.DrawTextStyled(
                    surface.Target,
                    PlayfieldRenderer.DrawArea(PlayfieldRenderer.TextScreenBounds(text, view)),
                    text.Text,
                    Faint(PlayfieldRenderer.TextColor),
                    Math.Max(1, (int)MathF.Round(text.Size * view.Scale)));
                break;

            case EditorPlacementMode.Grass:
                GrassPatch grass = placement.Grass;
                grass.Position = GridPlacement.PlaceGrass(pointer, grass, playfield, snap);
                grass.Color = Faint(grass.Color);
                PlayfieldRenderer.DrawGrassPatch(surface.Canvas, surface.Target, grass, view, frame.Seconds);
                break;

            case EditorPlacementMode.Flare:
                LevelFlare flare = placement.Flare;
                flare.Position = GridPlacement.PlaceFlare(pointer, flare, playfield, snap);
                PlayfieldRenderer.DrawFlareLight(surface.Canvas, surface.Target, flare, view, Faint(flare.Color));
                break;

            case EditorPlacementMode.Polygon:
                surface.Canvas.DrawCircle(surface.Target, ghost.Screen, 3f, Faint(PlayfieldRenderer.PolygonFill));
                break;

            case EditorPlacementMode.ScreenRead:
                ScreenReadRegion region = ScreenReadRegion.Centred(pointer, ScreenReadRegion.StartSize(view.ScreenSize / view.Scale));
                region.Position = GridPlacement.Settle(region.Position, region.Size, playfield, snap);
                var readRect = new Rect2(
                    view.PlayfieldToScreen(region.Position),
                    region.Size * view.Scale);
                surface.Canvas.DrawBorder(surface.Target, readRect, Faint(Palette.Info), 2f);
                surface.Canvas.DrawTextLeft(
                    surface.Target,
                    readRect.Grow(-6),
                    "SCREEN READ",
                    Faint(Palette.Info));
                break;

            case EditorPlacementMode.Start:
                PlayfieldRenderer.DrawStartPointBody(
                    surface.Canvas,
                    surface.Target,
                    ghost.Screen,
                    PlayfieldRenderer.StartPointRadius(view),
                    Faint(PlayfieldRenderer.StartPointColor));
                break;

            default:
                Vector2 notePoint = snap ? PlayfieldGrid.NearestCrossing(pointer, playfield) : pointer;
                Note note = EditorPlacementNote.Build(editor, frame.Playhead.TimeMs, notePoint);
                if (note.Kind == NoteKind.Dash)
                {
                    PlayfieldRenderer.DrawDashLink(
                        surface.Canvas,
                        surface.Target,
                        note,
                        view,
                        PlayfieldRenderer.NoteSize * view.Scale / 2f,
                        Faint(PlayfieldRenderer.ColorFor(note)));
                }

                PlayfieldRenderer.DrawNoteBody(
                    surface.Canvas,
                    surface.Target,
                    note,
                    view.PlayfieldToScreen(notePoint),
                    PlayfieldRenderer.NoteSize * view.Scale / 2f,
                    Faint(PlayfieldRenderer.ColorFor(note)));
                break;
        }
    }

    /// <summary>
    /// The outline the polygon tool is laying: its edges, a faint edge on to
    /// the pointer, and a ring on the first point that lights up once a click
    /// there would close it.
    /// </summary>
    private void DrawPolygonDraft(Entity editor, PlayfieldView view)
    {
        if (!editor.TryGetComponent(out EditorPolygonDraft draft) || !draft.Active)
            return;

        var points = new Vector2[draft.Points.Count];
        for (int i = 0; i < points.Length; i++)
            points[i] = view.PlayfieldToScreen(draft.Points[i]);

        if (points.Length >= 3)
            surface.Canvas.DrawPolygon(surface.Target, points, Faint(Faint(PlayfieldRenderer.PolygonFill)));
        if (points.Length >= 2)
            surface.Canvas.DrawPolyline(surface.Target, points, PlayfieldRenderer.PolygonFill, 2f);

        bool closable = false;
        if (editor.TryGetComponent(out EditorPlacementGhost ghost) && ghost.Visible)
        {
            surface.Canvas.DrawLine(
                surface.Target,
                points[^1],
                ghost.Screen,
                Faint(PlayfieldRenderer.PolygonFill),
                2f);
            closable = points.Length >= 3 &&
                       points[0].DistanceTo(ghost.Screen) <= EditorPolygonTool.CloseRadius;
        }

        foreach (Vector2 point in points)
            surface.Canvas.DrawCircle(surface.Target, point, 3f, PlayfieldRenderer.PolygonFill);
        surface.Canvas.DrawCircleOutline(
            surface.Target,
            points[0],
            EditorPolygonTool.CloseRadius,
            closable ? Palette.Accent : Faint(PlayfieldRenderer.PolygonFill),
            2f);
    }

    private static Color Faint(Color color) => new(color.R, color.G, color.B, color.A * GhostAlpha);
}
