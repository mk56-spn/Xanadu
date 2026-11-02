using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>A faint copy of what a click would place, under the pointer.</summary>
public sealed partial class EditorPlayfieldDrawSystem
{
    private const float GhostAlpha = 0.45f;

    private void DrawPlacementGhost(Entity editor, PlayfieldView view, Playfield playfield, Playhead playhead)
    {
        if (!editor.TryGetComponent(out EditorPlacementGhost ghost) ||
            !ghost.Visible ||
            !editor.TryGetComponent(out EditorPlacementModeState mode))
        {
            return;
        }

        Vector2 pointer = view.ScreenToPlayfield(ghost.Screen);
        switch (mode.Mode)
        {
            case EditorPlacementMode.Blocks:
                Vector2 size = BlockGrid.DefaultSize(playfield);
                Vector2 corner = BlockGrid.PlaceAt(pointer, size, playfield, BlockSnap.IsOn(_entities));
                var block = new Rect2(view.PlayfieldToScreen(corner), size * view.Scale);
                _canvas.DrawRect(_editor, block, Faint(PlayfieldRenderer.BlockFill));
                PlayfieldRenderer.DrawBorder(_canvas, _editor, block, Faint(PlayfieldRenderer.BlockEdge), 1.5f);
                break;

            case EditorPlacementMode.Spikes:
                int turns = editor.TryGetComponent(out EditorSpikeBrush brush) ? brush.QuarterTurns : 0;
                SpikeTriangle triangle = SpikeRules.ScreenTriangle(
                    SpikeRules.TileAt(pointer, playfield, turns),
                    view);
                Span<Vector2> points = [triangle.Tip, triangle.BaseLeft, triangle.BaseRight];
                _canvas.DrawPolygon(_editor, points, Faint(PlayfieldRenderer.SpikeColor));
                break;

            case EditorPlacementMode.Mines:
                _canvas.DrawCircle(
                    _editor,
                    ghost.Screen,
                    PlayfieldRenderer.MineSize * view.Scale / 2f,
                    Faint(Palette.DangerBright));
                break;

            case EditorPlacementMode.Start:
                _canvas.DrawCircleOutline(
                    _editor,
                    ghost.Screen,
                    PlayfieldRenderer.NoteSize * view.Scale / 2f,
                    Faint(Palette.Accent),
                    2f);
                break;

            default:
                Note note = EditorPlacementNote.Build(editor, playhead.TimeMs, pointer);
                PlayfieldRenderer.DrawNoteBody(
                    _canvas,
                    _editor,
                    note,
                    ghost.Screen,
                    PlayfieldRenderer.NoteSize * view.Scale / 2f,
                    Faint(PlayfieldRenderer.ColorFor(note)));
                break;
        }
    }

    private static Color Faint(Color color) => new(color.R, color.G, color.B, color.A * GhostAlpha);
}
