using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>Turning and mirroring the selection, and pasting geometry.</summary>
public sealed partial class EditorCommandSystem
{
    /// <summary>
    /// Turns the selection a quarter turn clockwise around its centre as one
    /// undo step; new spikes then paint the way the first selected one points.
    /// With nothing selected, spike mode turns the brush alone.
    /// </summary>
    private void RotateSelection(Entity editor)
    {
        if (!TryCaptureSelection(editor, out List<RotationStart> starts, out Vector2 pivot))
        {
            bool placing = editor.TryGetComponent(out EditorPlacementModeState mode) &&
                           mode.Mode == EditorPlacementMode.Spikes;
            if (placing && editor.HasComponent<EditorSpikeBrush>())
            {
                ref EditorSpikeBrush brush = ref editor.GetComponent<EditorSpikeBrush>();
                brush.QuarterTurns = SpikeRules.NormalizeTurns(brush.QuarterTurns + 1);
            }

            return;
        }

        Playfield playfield = editor.GetComponent<Playfield>();
        EditorSnapshot before = _history.Capture();
        foreach (RotationStart start in starts)
            SelectionRotation.Apply(start, pivot, Mathf.Pi / 2, playfield, BlockSnap.IsOn(_entities));
        _history.Record(before, _history.Capture());

        SyncSpikeBrush(editor, starts);
    }

    /// <summary>Mirrors the selection across its centre as one undo step.</summary>
    private void FlipSelection(Entity editor, bool horizontal)
    {
        if (!TryCaptureSelection(editor, out List<RotationStart> starts, out Vector2 pivot))
            return;

        Playfield playfield = editor.GetComponent<Playfield>();
        EditorSnapshot before = _history.Capture();
        foreach (RotationStart start in starts)
            SelectionRotation.ApplyFlip(start, pivot, horizontal, playfield, BlockSnap.IsOn(_entities));
        _history.Record(before, _history.Capture());

        SyncSpikeBrush(editor, starts);
    }

    private static bool TryCaptureSelection(
        Entity editor,
        out List<RotationStart> starts,
        out Vector2 pivot)
    {
        starts = new List<RotationStart>();
        pivot = default;
        if (!editor.HasComponent<NoteSelection>() || !editor.HasComponent<Playfield>())
            return false;

        IReadOnlyList<Entity> selection = editor.GetComponent<NoteSelection>().AllNotes;
        if (!SelectionRotation.TryGetItemBounds(selection, out Rect2 bounds))
            return false;

        pivot = bounds.GetCenter();
        foreach (Entity entity in selection)
        {
            if (EditorItems.IsItem(entity) && EditorItemPosition.TryGet(entity, out _))
                starts.Add(SelectionRotation.Capture(entity));
        }

        return true;
    }

    private static void SyncSpikeBrush(Entity editor, List<RotationStart> starts)
    {
        if (!editor.HasComponent<EditorSpikeBrush>())
            return;

        foreach (RotationStart start in starts)
        {
            if (start.Entity.HasComponent<Spike>())
            {
                editor.GetComponent<EditorSpikeBrush>().QuarterTurns =
                    start.Entity.GetComponent<Spike>().QuarterTurns;
                return;
            }
        }
    }

    /// <summary>
    /// Pastes a block or spike one grid cell further along per paste, since
    /// geometry is not timed and would otherwise land exactly on its original.
    /// A pasted block leaves its Form shape, so a copy never joins a construction.
    /// Returns null when a spike tile is already there.
    /// </summary>
    private Entity PasteGeometry(in ClipboardItem item, Entity editor)
    {
        Playfield playfield = editor.GetComponent<Playfield>();
        Vector2 offset = PlayfieldGrid.Cell(playfield) * _pasteCount;
        Entity pasted;

        if (item.Block is { } block)
        {
            block.Position = BlockGrid.Clamp(block.Position + offset, block.Size, playfield);
            block.ShapeId = 0;
            block.ShapePiece = 0;
            pasted = EditorItems.Create(_entities, block, item.Shader);
        }
        else
        {
            Spike spike = item.Spike!.Value;
            spike.Position = SpikeRules.Clamp(spike.Position + offset, spike.Size, playfield);
            foreach (Entity other in _entities.Query<Spike>().ToEntityList())
            {
                if (other.GetComponent<Spike>().Bounds.Intersects(spike.Bounds))
                    return default;
            }

            pasted = EditorItems.Create(_entities, spike, item.Shader);
        }

        Subscribe(pasted, item.Groups);
        SelfModulate.Put(pasted, item.Modulate);
        return pasted;
    }
}
