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
    /// With nothing selected, the block and spike tools turn the brush alone.
    /// </summary>
    private void RotateSelection(Entity editor)
    {
        if (!TryCaptureSelection(editor, out List<RotationStart> starts, out Vector2 pivot))
        {
            bool placing = editor.TryGetComponent(out EditorPlacementModeState mode) &&
                           mode.Mode is EditorPlacementMode.Spikes or EditorPlacementMode.Blocks;
            if (placing && editor.HasComponent<EditorPlacementBrush>())
            {
                ref EditorPlacementBrush brush = ref editor.GetComponent<EditorPlacementBrush>();
                brush.Degrees = SpikeRules.NormalizeDegrees(brush.Degrees + 90f);
            }

            return;
        }

        Playfield playfield = editor.GetComponent<Playfield>();
        EditorSnapshot before = _history.Capture();
        foreach (RotationStart start in starts)
            SelectionRotation.Apply(start, pivot, Mathf.Pi / 2, playfield, ViewportSnap.IsOn(_entities));
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
            SelectionRotation.ApplyFlip(start, pivot, horizontal, playfield, ViewportSnap.IsOn(_entities));
        _history.Record(before, _history.Capture());

        SyncSpikeBrush(editor, starts);
    }

    /// <summary>
    /// Steps the selected blocks through solid, then one-way up, right, down and
    /// left, as one undo step. They all take the side after the first one's, so
    /// a mixed selection ends up matching. Other selected items are left alone.
    /// </summary>
    private void CycleOneWay(Entity editor)
    {
        if (!editor.HasComponent<NoteSelection>())
            return;

        var blocks = new List<Entity>();
        foreach (Entity entity in editor.GetComponent<NoteSelection>().AllNotes)
        {
            if (EditorItems.IsItem(entity) && entity.HasComponent<Block>())
                blocks.Add(entity);
        }

        if (blocks.Count == 0)
            return;

        BlockOneWay next = blocks[0].GetComponent<Block>().OneWay == BlockOneWay.Left
            ? BlockOneWay.Off
            : blocks[0].GetComponent<Block>().OneWay + 1;
        EditorSnapshot before = _history.Capture();
        foreach (Entity block in blocks)
            block.GetComponent<Block>().OneWay = next;
        _history.Record(before, _history.Capture());
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
        if (!editor.HasComponent<EditorPlacementBrush>())
            return;

        foreach (RotationStart start in starts)
        {
            if (start.Entity.HasComponent<Spike>())
            {
                editor.GetComponent<EditorPlacementBrush>().Degrees =
                    start.Entity.GetComponent<Spike>().RotationDegrees;
                return;
            }
        }
    }

    /// <summary>
    /// Pastes a block or spike one grid cell further along per paste, since
    /// geometry is not timed and would otherwise land exactly on its original.
    /// A pasted block leaves its Form shape, so a copy never joins a construction.
    /// Returns null when one of its kind is already there.
    /// </summary>
    private Entity PasteGeometry(in ClipboardItem item, Entity editor)
    {
        Playfield playfield = editor.GetComponent<Playfield>();
        Vector2 offset = PlayfieldGrid.Cell(playfield) * _pasteCount;
        Entity pasted;

        if (item.Block is { } block)
        {
            block.Position = GridPlacement.Clamp(block.Position + offset, block.Size, playfield);
            block.ShapeId = 0;
            block.ShapePiece = 0;
            if (!GridPlacement.IsBlockSpaceFree(_entities, block.Bounds))
                return default;

            pasted = EditorItems.Create(_entities, block, item.Shader);
        }
        else
        {
            Spike spike = item.Spike!.Value;
            spike.Position = GridPlacement.Clamp(spike.Position + offset, spike.Size, playfield);
            if (!GridPlacement.IsSpikeSpaceFree(_entities, spike.Bounds))
                return default;

            pasted = EditorItems.Create(_entities, spike, item.Shader);
        }

        Subscribe(pasted, item.Groups);
        SelfModulate.Put(pasted, item.Modulate);
        return pasted;
    }
}
