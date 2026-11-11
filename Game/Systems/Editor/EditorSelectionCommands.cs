using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;
using Xanadu.Timeline;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The editor's selection commands: copying and pasting, deleting, moving in
/// time, turning and mirroring, and the one-way block cycle.
/// </summary>
public sealed class EditorSelectionCommands(EditorCommandContext context)
{
    private readonly List<ItemCopy> _clipboard = new();
    private int _pasteCount;

    public bool TryApply(EditorCommand command, Entity editor, CommandBuffer buffer)
    {
        switch (command.Type)
        {
            case EditorCommandType.DeleteSelection:
                DeleteSelection(editor);
                return true;

            case EditorCommandType.RotateSelection:
                RotateSelection(editor);
                return true;

            case EditorCommandType.FlipSelection:
                FlipSelection(editor, command.IntValue != 0);
                return true;

            case EditorCommandType.CycleOneWay:
                CycleOneWay(editor);
                return true;

            case EditorCommandType.CopySelection:
                CopySelection(editor);
                return true;

            case EditorCommandType.PasteSelection:
                PasteSelection(editor, buffer);
                return true;

            case EditorCommandType.SetTarget:
                SetTarget(command.IntValue, editor);
                return true;

            case EditorCommandType.MoveSelection:
                MoveSelection(command.IntValue, editor);
                return true;

            case EditorCommandType.ZoomTimeline:
                ZoomTimeline(command.IntValue, editor);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Turns the selection a quarter turn clockwise around its centre as one
    /// undo step; new spikes then paint the way the first selected one points.
    /// With nothing selected, the block and spike tools turn the brush alone.
    /// </summary>
    private void RotateSelection(Entity editor)
    {
        if (!TryCaptureSelection(editor, out List<RotationStart> starts, out Vector2 pivot))
        {
            if (editor.HasComponent<EditorPlacement>())
            {
                ref EditorPlacement brush = ref editor.GetComponent<EditorPlacement>();
                if (brush.Mode is EditorPlacementMode.Spikes or EditorPlacementMode.Blocks)
                    brush.Degrees = SpikeRules.NormalizeDegrees(brush.Degrees + 90f);
            }

            return;
        }

        Playfield playfield = editor.GetComponent<Playfield>();
        EditorSnapshot before = context.History.Capture();
        foreach (RotationStart start in starts)
            SelectionRotation.Apply(start, pivot, Mathf.Pi / 2, playfield, ViewportSnap.IsOn(context.Entities));
        context.History.Record(before, context.History.Capture());

        SyncSpikeBrush(editor, starts);
    }

    /// <summary>Mirrors the selection across its centre as one undo step.</summary>
    private void FlipSelection(Entity editor, bool horizontal)
    {
        if (!TryCaptureSelection(editor, out List<RotationStart> starts, out Vector2 pivot))
            return;

        Playfield playfield = editor.GetComponent<Playfield>();
        EditorSnapshot before = context.History.Capture();
        foreach (RotationStart start in starts)
            SelectionRotation.ApplyFlip(start, pivot, horizontal, playfield, ViewportSnap.IsOn(context.Entities));
        context.History.Record(before, context.History.Capture());

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
        EditorSnapshot before = context.History.Capture();
        foreach (Entity block in blocks)
            block.GetComponent<Block>().OneWay = next;
        context.History.Record(before, context.History.Capture());
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
        if (!editor.HasComponent<EditorPlacement>())
            return;

        foreach (RotationStart start in starts)
        {
            if (start.Entity.HasComponent<Spike>())
            {
                editor.GetComponent<EditorPlacement>().Degrees =
                    start.Entity.GetComponent<Spike>().RotationDegrees;
                return;
            }
        }
    }

    private void MoveSelection(int direction, Entity editor)
    {
        if (direction == 0 ||
            !editor.HasComponent<NoteSelection>() ||
            !editor.HasComponent<Playhead>())
        {
            return;
        }

        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();
        var items = new List<Entity>();
        foreach (Entity entity in selection.AllNotes)
        {
            if (EditorItems.IsItem(entity) &&
                EditorItemTiming.TryGet(entity, out _))
            {
                items.Add(entity);
            }
        }

        if (items.Count == 0)
            return;

        Playhead playhead = editor.GetComponent<Playhead>();
        // A step in beats, so items keep their place on the grid across tempo changes.
        double stepBeats = EditorTiming.StepBeats(playhead) * Math.Sign(direction);
        Xanadu.Levels.TempoMap tempo = playhead.Tempo;
        long maxTime = playhead.DurationMs > 0
            ? playhead.DurationMs
            : long.MaxValue;
        EditorSnapshot before = context.History.Capture();
        bool changed = false;

        foreach (Entity entity in items)
        {
            if (!EditorItemTiming.TryGet(entity, out long timeMs))
                continue;

            long moved = Math.Clamp(
                (long)Math.Round(tempo.TimeAt(tempo.BeatAt(timeMs) + stepBeats)),
                0,
                maxTime);
            if (moved == timeMs)
                continue;

            EditorItemTiming.Set(entity, moved);
            changed = true;
        }

        if (!changed)
            return;

        context.History.Record(before, context.History.Capture());
    }

    private static void ZoomTimeline(int direction, Entity editor)
    {
        if (!editor.HasComponent<TimelineView>())
            return;

        ref TimelineView timeline = ref editor.GetComponent<TimelineView>();
        timeline.Zoom = TimelineZoom.Step(timeline.Zoom, direction);
        timeline.AutoZoomed = true;
        // The buttons zoom about the playhead, so any pointer pin lets go.
        timeline.Pinned = false;
    }

    private static void SetTarget(int value, Entity editor)
    {
        int index = Math.Clamp(value, 0, KeybindSettings.Actions.Length - 1);
        KeybindAction target = KeybindSettings.Actions[index];

        if (editor.HasComponent<EditorPlacement>())
            editor.GetComponent<EditorPlacement>().Note = target;
    }

    private void CopySelection(Entity editor)
    {
        if (!editor.HasComponent<NoteSelection>())
            return;

        _clipboard.Clear();
        _pasteCount = 0;
        foreach (Entity entity in editor.GetComponent<NoteSelection>().AllNotes)
        {
            if (EditorItems.IsItem(entity) && ItemKinds.Of(entity) is { } kind)
                _clipboard.Add(kind.Copy(entity));
        }
    }

    private void PasteSelection(Entity editor, CommandBuffer buffer)
    {
        if (_clipboard.Count == 0 || !editor.HasComponent<NoteSelection>())
            return;

        // A paste that would pass the object cap is refused whole, never cut short.
        if (EditorItems.WouldOverfill(editor.Store, editor, _clipboard.Count))
            return;

        ref Playhead playhead = ref editor.GetComponent<Playhead>();

        // Geometry is not timed, so only timed items set the time anchor.
        long anchor = long.MaxValue;
        foreach (ItemCopy item in _clipboard)
        {
            if (item.Kind.TimeOf(item.Value) is { } time)
                anchor = Math.Min(anchor, time);
        }

        // The paste lands on the snap nearest the playhead, so a copy made on the
        // beat stays on the beat; the items keep their spacing from each other.
        long target = EditorTiming.Snap(Math.Max(0, playhead.TimeMs), playhead);
        long maxTime = playhead.DurationMs > 0
            ? playhead.DurationMs
            : long.MaxValue;
        // Geometry is not timed either, so it goes a grid cell further along per paste
        // rather than landing exactly on its original.
        Playfield playfield = editor.GetComponent<Playfield>();
        Vector2 offset = PlayfieldGrid.Cell(playfield) * (_pasteCount + 1);
        EditorSnapshot before = context.History.Capture();
        var created = new List<Entity>();
        _pasteCount++;

        foreach (ItemCopy item in _clipboard)
        {
            long timeMs = item.Kind.TimeOf(item.Value) is { } itemTime
                ? Math.Clamp(target + (itemTime - anchor), 0, maxTime)
                : 0;
            // Null when collision is on and one of its kind is already there.
            if (item.Kind.Pasted(item.Value, offset, timeMs, playfield, context.Entities) is not { } value)
                continue;

            created.Add(item.Kind.Restore(context.Entities, item.WithValue(value), buffer));
        }

        if (created.Count == 0)
            return;

        // The copies keep their layers and their order among themselves, in front of what was there.
        ItemLayer.OnTop(context.Entities, created);
        editor.GetComponent<NoteSelection>().SetMany(created);
        context.History.Record(before, context.History.Capture());
    }

    private void DeleteSelection(Entity editor)
    {
        if (!editor.HasComponent<NoteSelection>())
            return;

        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();
        var items = new List<Entity>();
        foreach (Entity entity in selection.AllNotes)
        {
            if (EditorItems.IsItem(entity))
                items.Add(entity);
        }

        if (items.Count == 0)
        {
            selection.Clear();
            return;
        }

        EditorSnapshot before = context.History.Capture();
        foreach (Entity entity in items)
            entity.DeleteEntity();

        context.History.Record(before, context.History.Capture());
        selection.Clear();
    }

    /// <summary>Drops the selection after a snapshot is restored.</summary>
    public static void ClearSelection(Entity editor)
    {
        if (editor.HasComponent<NoteSelection>())
            editor.GetComponent<NoteSelection>().Clear();
    }
}
