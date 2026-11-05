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
public sealed partial class EditorPlayfieldDrawSystem
{
    private const float GhostAlpha = 0.45f;

    private void DrawPlacementGhost(Entity editor, PlayfieldView view, Playfield playfield, Playhead playhead)
    {
        DrawPolygonDraft(editor, view);
        if (!editor.TryGetComponent(out EditorPlacementGhost ghost) ||
            !ghost.Visible ||
            !editor.TryGetComponent(out EditorPlacement placement))
        {
            return;
        }

        Vector2 pointer = view.ScreenToPlayfield(ghost.Screen);
        float degrees = placement.Degrees;
        bool snap = ViewportSnap.IsOn(_entities);
        switch (placement.Mode)
        {
            case EditorPlacementMode.Blocks:
                Vector2 size = GridPlacement.NewBlockSize(playfield, degrees, placement.BlockSize);
                Vector2 corner = GridPlacement.Place(pointer, size, playfield, snap);
                var block = new Rect2(view.PlayfieldToScreen(corner), size * view.Scale);
                _canvas.DrawRect(_editor, block, Faint(PlayfieldRenderer.BlockFill));
                _canvas.DrawBorder(_editor, block, Faint(Block.DefaultOutlineColor), Block.DefaultOutlineWidth * view.Scale);
                break;

            case EditorPlacementMode.Spikes:
                Vector2 tile = GridPlacement.NewSpikeSize(playfield);
                SpikeTriangle triangle = SpikeRules.ScreenTriangle(
                    new Spike(GridPlacement.Place(pointer, tile, playfield, snap), tile, degrees),
                    view);
                PlayfieldRenderer.DrawSpikeBody(
                    _canvas,
                    _editor,
                    triangle,
                    Block.DefaultOutlineWidth * view.Scale,
                    Faint(PlayfieldRenderer.SpikeColor),
                    Faint(Block.DefaultOutlineColor));
                break;

            case EditorPlacementMode.Mines:
                _canvas.DrawCircle(
                    _editor,
                    ghost.Screen,
                    PlayfieldRenderer.MineSize * view.Scale / 2f,
                    Faint(Palette.DangerBright));
                break;

            case EditorPlacementMode.Text:
                var text = new LevelText(pointer, LevelText.Placeholder);
                _canvas.DrawTextStyled(
                    _editor,
                    PlayfieldRenderer.DrawArea(PlayfieldRenderer.TextScreenBounds(text, view)),
                    text.Text,
                    Faint(PlayfieldRenderer.TextColor),
                    Math.Max(1, (int)MathF.Round(text.Size * view.Scale)));
                break;

            case EditorPlacementMode.Grass:
                GrassPatch grass = placement.Grass;
                grass.Position = snap ? PlayfieldGrid.NearestCrossing(pointer, playfield) : pointer;
                grass.Color = Faint(grass.Color);
                PlayfieldRenderer.DrawGrassPatch(_canvas, _editor, grass, view, _seconds);
                break;

            case EditorPlacementMode.Polygon:
                _canvas.DrawCircle(_editor, ghost.Screen, 3f, Faint(PlayfieldRenderer.PolygonFill));
                break;

            case EditorPlacementMode.Start:
                PlayfieldRenderer.DrawStartPointBody(
                    _canvas,
                    _editor,
                    ghost.Screen,
                    PlayfieldRenderer.StartPointRadius(view),
                    Faint(PlayfieldRenderer.StartPointColor));
                break;

            default:
                Vector2 notePoint = snap ? PlayfieldGrid.NearestCrossing(pointer, playfield) : pointer;
                Note note = EditorPlacementNote.Build(editor, playhead.TimeMs, notePoint);
                if (note.Kind == NoteKind.Dash)
                {
                    PlayfieldRenderer.DrawDashLink(
                        _canvas,
                        _editor,
                        note,
                        view,
                        PlayfieldRenderer.NoteSize * view.Scale / 2f,
                        Faint(PlayfieldRenderer.ColorFor(note)));
                }

                PlayfieldRenderer.DrawNoteBody(
                    _canvas,
                    _editor,
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
            _canvas.DrawPolygon(_editor, points, Faint(Faint(PlayfieldRenderer.PolygonFill)));
        if (points.Length >= 2)
            _canvas.DrawPolyline(_editor, points, PlayfieldRenderer.PolygonFill, 2f);

        bool closable = false;
        if (editor.TryGetComponent(out EditorPlacementGhost ghost) && ghost.Visible)
        {
            _canvas.DrawLine(_editor, points[^1], ghost.Screen, Faint(PlayfieldRenderer.PolygonFill), 2f);
            closable = points.Length >= 3 &&
                       points[0].DistanceTo(ghost.Screen) <= EditorNoteInputSystem.PolygonCloseRadius;
        }

        foreach (Vector2 point in points)
            _canvas.DrawCircle(_editor, point, 3f, PlayfieldRenderer.PolygonFill);
        _canvas.DrawCircleOutline(
            _editor,
            points[0],
            EditorNoteInputSystem.PolygonCloseRadius,
            closable ? Palette.Accent : Faint(PlayfieldRenderer.PolygonFill),
            2f);
    }

    private static Color Faint(Color color) => new(color.R, color.G, color.B, color.A * GhostAlpha);
}
