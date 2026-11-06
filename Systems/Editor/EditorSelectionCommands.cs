using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
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
    private readonly List<EditorClipboardItem> _clipboard = new();
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
        double step = EditorTiming.StepMs(playhead);
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
                timeMs + (long)Math.Round(step) * Math.Sign(direction),
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
            if (!EditorItems.IsItem(entity))
                continue;

            if (entity.HasComponent<Note>())
            {
                Note note = entity.GetComponent<Note>();
                _clipboard.Add(new EditorClipboardItem(note.TimeMs, note, null, null, MaskOf(entity), ItemShader.Of(entity), SelfModulate.Packed(entity), Layer: ItemLayer.Of(entity), Blend: ItemBlend.Of(entity), ShaderSource: ItemShaderSource.Of(entity)));
            }
            else if (entity.HasComponent<Mine>())
            {
                Mine mine = entity.GetComponent<Mine>();
                _clipboard.Add(new EditorClipboardItem(mine.TimeMs, null, mine, null, MaskOf(entity), ItemShader.Of(entity), SelfModulate.Packed(entity), Layer: ItemLayer.Of(entity), Blend: ItemBlend.Of(entity), ShaderSource: ItemShaderSource.Of(entity)));
            }
            else if (entity.HasComponent<StartPoint>())
            {
                StartPoint start = entity.GetComponent<StartPoint>();
                _clipboard.Add(new EditorClipboardItem(start.TimeMs, null, null, start, MaskOf(entity), Layer: ItemLayer.Of(entity), Blend: ItemBlend.Of(entity), ShaderSource: ItemShaderSource.Of(entity)));
            }
            else if (entity.HasComponent<Block>())
            {
                _clipboard.Add(new EditorClipboardItem(0, null, null, null, MaskOf(entity), ItemShader.Of(entity), SelfModulate.Packed(entity), Block: entity.GetComponent<Block>(), Layer: ItemLayer.Of(entity), Blend: ItemBlend.Of(entity), ShaderSource: ItemShaderSource.Of(entity)));
            }
            else if (entity.HasComponent<Spike>())
            {
                _clipboard.Add(new EditorClipboardItem(0, null, null, null, MaskOf(entity), ItemShader.Of(entity), SelfModulate.Packed(entity), Spike: entity.GetComponent<Spike>(), Layer: ItemLayer.Of(entity), Blend: ItemBlend.Of(entity), ShaderSource: ItemShaderSource.Of(entity)));
            }
            else if (entity.HasComponent<LevelText>())
            {
                _clipboard.Add(new EditorClipboardItem(0, null, null, null, MaskOf(entity), ItemShader.Of(entity), SelfModulate.Packed(entity), Text: entity.GetComponent<LevelText>(), Layer: ItemLayer.Of(entity), Blend: ItemBlend.Of(entity), ShaderSource: ItemShaderSource.Of(entity)));
            }
            else if (entity.HasComponent<LevelPolygon>())
            {
                LevelPolygon polygon = entity.GetComponent<LevelPolygon>();
                polygon.Item = Xanadu.Items.XifCopy.Clone(polygon.Item ?? new Xanadu.Items.Xif());
                _clipboard.Add(new EditorClipboardItem(0, null, null, null, MaskOf(entity), ItemShader.Of(entity), SelfModulate.Packed(entity), Polygon: polygon, Layer: ItemLayer.Of(entity), Blend: ItemBlend.Of(entity), ShaderSource: ItemShaderSource.Of(entity)));
            }
            else if (entity.HasComponent<GrassPatch>())
            {
                _clipboard.Add(new EditorClipboardItem(0, null, null, null, MaskOf(entity), ItemShader.Of(entity), SelfModulate.Packed(entity), Grass: entity.GetComponent<GrassPatch>(), Layer: ItemLayer.Of(entity), Blend: ItemBlend.Of(entity), ShaderSource: ItemShaderSource.Of(entity)));
            }
            else if (entity.HasComponent<ScreenReadRegion>())
            {
                _clipboard.Add(new EditorClipboardItem(0, null, null, null, MaskOf(entity), Layer: ItemLayer.Of(entity), Region: entity.GetComponent<ScreenReadRegion>()));
            }
        }
    }

    private void PasteSelection(Entity editor, CommandBuffer buffer)
    {
        if (_clipboard.Count == 0 || !editor.HasComponent<NoteSelection>())
            return;

        ref Playhead playhead = ref editor.GetComponent<Playhead>();

        // Geometry is not timed, so only timed items set the time anchor.
        long anchor = _clipboard.Exists(item => !item.IsGeometry)
            ? _clipboard.Where(item => !item.IsGeometry).Min(item => item.TimeMs)
            : 0;
        // The paste lands on the snap nearest the playhead, so a copy made on the
        // beat stays on the beat; the items keep their spacing from each other.
        long target = EditorTiming.Snap(Math.Max(0, playhead.TimeMs), playhead);
        long maxTime = playhead.DurationMs > 0
            ? playhead.DurationMs
            : long.MaxValue;
        EditorSnapshot before = context.History.Capture();
        var created = new List<Entity>();
        _pasteCount++;

        foreach (EditorClipboardItem item in _clipboard)
        {
            if (item.IsGeometry)
            {
                Entity geometry = PasteGeometry(item, editor, buffer);
                if (!geometry.IsNull)
                    created.Add(geometry);
                continue;
            }

            long time = Math.Clamp(
                target + (item.TimeMs - anchor),
                0,
                maxTime);
            if (item.Note is { } note)
            {
                note.TimeMs = time;
                Entity pasted = EditorItems.Create(context.Entities, note, item.Shader);
                Subscribe(pasted, item.Groups, buffer);
                SelfModulate.Put(pasted, item.Modulate);
                ItemLayer.Put(pasted, item.Layer);
                ItemBlend.Put(pasted, item.Blend);
                ItemShaderSource.Put(pasted, item.ShaderSource);
                created.Add(pasted);
            }
            else if (item.Mine is { } mine)
            {
                mine.TimeMs = time;
                Entity pasted = EditorItems.Create(context.Entities, mine, item.Shader);
                Subscribe(pasted, item.Groups, buffer);
                SelfModulate.Put(pasted, item.Modulate);
                ItemLayer.Put(pasted, item.Layer);
                ItemBlend.Put(pasted, item.Blend);
                ItemShaderSource.Put(pasted, item.ShaderSource);
                created.Add(pasted);
            }
            else if (item.StartPoint is { } start)
            {
                start.TimeMs = time;
                start.Groups = item.Groups;
                Entity pasted = EditorItems.Create(context.Entities, start);
                Subscribe(pasted, item.Groups, buffer);
                ItemLayer.Put(pasted, item.Layer);
                ItemBlend.Put(pasted, item.Blend);
                ItemShaderSource.Put(pasted, item.ShaderSource);
                created.Add(pasted);
            }
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

    /// <summary>
    /// Pastes a block or spike one grid cell further along per paste, since
    /// geometry is not timed and would otherwise land exactly on its original.
    /// A pasted block leaves its Form shape, so a copy never joins a construction.
    /// Returns null when collision is on and one of its kind is already there.
    /// </summary>
    private Entity PasteGeometry(in EditorClipboardItem item, Entity editor, CommandBuffer buffer)
    {
        Playfield playfield = editor.GetComponent<Playfield>();
        Vector2 offset = PlayfieldGrid.Cell(playfield) * _pasteCount;
        Entity pasted;

        if (item.Block is { } block)
        {
            block.Position = GridPlacement.Clamp(block.Position + offset, block.Size, playfield);
            block.ShapeId = 0;
            block.ShapePiece = 0;
            if (!PlacementCollision.BlockFits(context.Entities, block.Bounds))
                return default;

            pasted = EditorItems.Create(context.Entities, block, item.Shader);
        }
        else if (item.Text is { } text)
        {
            text.Position += offset;
            pasted = EditorItems.Create(context.Entities, text, item.Shader);
        }
        else if (item.Polygon is { } polygon)
        {
            // Every paste gets its own copy of the shape, so editing one leaves the others.
            polygon.Position += offset;
            polygon.Item = Xanadu.Items.XifCopy.Clone(polygon.Item);
            pasted = EditorItems.Create(context.Entities, polygon, item.Shader);
        }
        else if (item.Grass is { } grass)
        {
            // A fresh seed, so the copy is the same kind of grass but not the same blades.
            grass.Position += offset;
            grass.Seed = GrassPatch.NewSeed();
            pasted = EditorItems.Create(context.Entities, grass, item.Shader);
        }
        else if (item.Region is { } region)
        {
            region.Position = GridPlacement.Clamp(region.Position + offset, region.Size, playfield);
            pasted = EditorItems.Create(context.Entities, region);
        }
        else
        {
            Spike spike = item.Spike!.Value;
            spike.Position = GridPlacement.Clamp(spike.Position + offset, spike.Size, playfield);
            if (!PlacementCollision.SpikeFits(context.Entities, spike.Bounds))
                return default;

            pasted = EditorItems.Create(context.Entities, spike, item.Shader);
        }

        Subscribe(pasted, item.Groups, buffer);
        SelfModulate.Put(pasted, item.Modulate);
        ItemLayer.Put(pasted, item.Layer);
                ItemBlend.Put(pasted, item.Blend);
                ItemShaderSource.Put(pasted, item.ShaderSource);
        return pasted;
    }

    /// <summary>Drops the selection after a snapshot is restored.</summary>
    public static void ClearSelection(Entity editor)
    {
        if (editor.HasComponent<NoteSelection>())
            editor.GetComponent<NoteSelection>().Clear();
    }

    private static uint MaskOf(Entity entity) =>
        entity.TryGetComponent(out ItemGroups groups) ? groups.Mask : 0u;

    private static void Subscribe(Entity item, uint mask, CommandBuffer buffer)
    {
        if (mask != 0)
            buffer.AddComponent(item.Id, new ItemGroups(mask));
    }
}
