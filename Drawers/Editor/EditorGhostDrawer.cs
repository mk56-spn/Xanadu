using Xanadu.Ui.Systems.Canvas;
using System;
using System.Collections.Generic;
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
                surface.Canvas.DrawRect(surface.Target, block, Faint(PlayfieldColors.BlockFill));
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
                    Faint(PlayfieldColors.SpikeColor),
                    Faint(Block.DefaultOutlineColor));
                break;

            case EditorPlacementMode.FrictionBar:
                var ghostBar = new FrictionBar(
                    GridPlacement.PlaceFrictionBar(pointer, GridPlacement.NewFrictionBarHeight(playfield), playfield, snap),
                    GridPlacement.NewFrictionBarHeight(playfield));
                surface.Canvas.DrawRect(
                    surface.Target,
                    PlayfieldMetrics.FrictionBarScreenBounds(ghostBar, view),
                    Faint(PlayfieldColors.FrictionBarColor));
                break;

            case EditorPlacementMode.Mines:
                surface.Canvas.DrawCircle(
                    surface.Target,
                    ghost.Screen,
                    PlayfieldMetrics.MineSize * view.Scale / 2f,
                    Faint(Palette.DangerBright));
                break;

            case EditorPlacementMode.Text:
                var text = new LevelText(pointer, LevelText.Placeholder);
                surface.Canvas.DrawTextStyled(
                    surface.Target,
                    PlayfieldRenderer.DrawArea(PlayfieldMetrics.TextScreenBounds(text, view)),
                    text.Text,
                    Faint(PlayfieldColors.TextColor),
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
                surface.Canvas.DrawCircle(surface.Target, ghost.Screen, 3f, Faint(PlayfieldColors.PolygonFill));
                break;

            case EditorPlacementMode.Curve:
                surface.Canvas.DrawCircle(surface.Target, ghost.Screen, 4f, Faint(PlayfieldColors.CurveColor));
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
                    PlayfieldMetrics.StartPointRadius(view),
                    Faint(PlayfieldColors.StartPointColor));
                break;

            default:
                Vector2 notePoint = snap ? PlayfieldGrid.NearestCrossing(pointer, playfield) : pointer;
                Note note = EditorPlacementNote.Build(editor, frame.Playhead.TimeMs, notePoint);
                PlayfieldRenderer.DrawNoteBody(
                    surface.Canvas,
                    surface.Target,
                    note,
                    view.PlayfieldToScreen(notePoint),
                    PlayfieldMetrics.NoteSize * view.Scale / 2f,
                    Faint(PlayfieldColors.ColorFor(note)));
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

        if (draft.Mode == EditorPlacementMode.Curve)
        {
            DrawCurveDraft(editor, view, draft);
            return;
        }

        var points = new Vector2[draft.Points.Count];
        for (int i = 0; i < points.Length; i++)
            points[i] = view.PlayfieldToScreen(draft.Points[i]);

        if (points.Length >= 3)
            surface.Canvas.DrawPolygon(surface.Target, points, Faint(Faint(PlayfieldColors.PolygonFill)));
        if (points.Length >= 2)
            surface.Canvas.DrawPolyline(surface.Target, points, PlayfieldColors.PolygonFill, 2f);

        bool closable = false;
        if (editor.TryGetComponent(out EditorPlacementGhost ghost) && ghost.Visible)
        {
            surface.Canvas.DrawLine(
                surface.Target,
                points[^1],
                ghost.Screen,
                Faint(PlayfieldColors.PolygonFill),
                2f);
            closable = points.Length >= 3 &&
                       points[0].DistanceTo(ghost.Screen) <= EditorPolygonDraft.CloseRadius;
        }

        foreach (Vector2 point in points)
            surface.Canvas.DrawCircle(surface.Target, point, 3f, PlayfieldColors.PolygonFill);
        surface.Canvas.DrawCircleOutline(
            surface.Target,
            points[0],
            EditorPolygonDraft.CloseRadius,
            closable ? Palette.Accent : Faint(PlayfieldColors.PolygonFill),
            2f);
    }

    /// <summary>
    /// The curve the curve tool is laying: the smooth path through its points
    /// and on to the pointer, its points ringed by speed, and a ring on the last
    /// point that lights up once a click there would finish it.
    /// </summary>
    private void DrawCurveDraft(Entity editor, PlayfieldView view, EditorPolygonDraft draft)
    {
        var points = new List<Vector2>(draft.Points);
        var speeds = new List<float>(draft.Speeds);
        Vector2 last = view.PlayfieldToScreen(draft.Points[^1]);
        bool finishable = false;
        if (editor.TryGetComponent(out EditorPlacementGhost ghost) && ghost.Visible)
        {
            finishable = draft.Points.Count >= NotePath.MinPoints &&
                         last.DistanceTo(ghost.Screen) <= EditorPolygonDraft.CloseRadius;
            if (!finishable)
            {
                points.Add(view.ScreenToPlayfield(ghost.Screen));
                speeds.Add(speeds[^1]);
            }
        }

        CurvePath path = CurvePath.Of(draft.StartMs, points, speeds);
        PlayfieldRenderer.DrawCurvePath(surface.Canvas, surface.Target, path.Points, view, Faint(PlayfieldColors.CurveColor), 3f);
        PlayfieldRenderer.DrawCurvePoints(surface.Canvas, surface.Target, draft.Points, draft.Speeds, view, PlayfieldColors.CurveColor);
        surface.Canvas.DrawCircleOutline(
            surface.Target,
            last,
            EditorPolygonDraft.CloseRadius,
            finishable ? Palette.Accent : Faint(PlayfieldColors.CurveColor),
            2f);
    }

    private static Color Faint(Color color) => new(color.R, color.G, color.B, color.A * GhostAlpha);
}
