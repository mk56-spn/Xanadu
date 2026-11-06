using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Rendering;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The key lane under the effect rows: the selected shader setting's keys as
/// pins on the timeline (docs/render-architecture.md step 8, "keys use the
/// shared timeline"). Clicking a pin selects it and moves the playhead to it;
/// dragging retimes it. The lane's table cells type the selected key's moment
/// and value, cycle its easing and delete it. Every change is one undo step.
/// </summary>
public sealed partial class EditorGroupSystem
{
    private static readonly EditorGroupPart[] KeyCells =
    {
        EditorGroupPart.KeyTime, EditorGroupPart.KeyValue, EditorGroupPart.KeyEase, EditorGroupPart.KeyDelete,
    };

    /// <summary>Whether a key of the setting sits at the selected moment.</summary>
    private static bool HasSelectedKey(EditorGroupState state, List<ShaderKey> keys, InstanceUniform uniform, out ShaderKey key)
    {
        int at = state.SelectedKey < 0f ? -1 : EditorShaderKeyEdits.Find(keys, uniform.Name, state.SelectedKey);
        key = at >= 0 ? keys[at] : default;
        return at >= 0;
    }

    /// <summary>EASE and X on the lane act on the selected key.</summary>
    private void HandleKeyLaneButtons(ref EditorGroupState state, List<ShaderKey> keys, InstanceUniform uniform)
    {
        bool ease = JustPressed(Piece(EditorGroupPart.KeyEase));
        bool delete = JustPressed(Piece(EditorGroupPart.KeyDelete));
        if (!HasSelectedKey(state, keys, uniform, out _))
        {
            state.SelectedKey = -1f;
            return;
        }

        float time = state.SelectedKey;
        if (ease)
            Edit(() => EditorShaderKeyEdits.CycleEase(keys, uniform.Name, time));
        if (delete)
        {
            Edit(() => EditorShaderKeyEdits.Delete(keys, uniform.Name, time));
            state.SelectedKey = -1f;
        }
    }

    /// <summary>
    /// A press on the lane: on a pin it selects that key, moves the playhead to
    /// it and starts retiming it; elsewhere it drops the key selection. False
    /// when the press wasn't on a pin.
    /// </summary>
    private bool BeginKeyDrag(
        Entity editor,
        ref EditorGroupState state,
        GroupDefinition group,
        Vector2 point,
        Rect2 lane)
    {
        if (!lane.Grow(2f).HasPoint(point))
            return false;

        state.SelectedKey = -1f;
        if (!TrySelectedSetting(editor, state, group, out InstanceUniform uniform, out List<ShaderKey> keys))
            return false;

        float best = EditorGroupLayout.KeyGrab;
        float? picked = null;
        foreach (ShaderKey key in EditorShaderKeyEdits.Lane(keys, uniform.Name))
        {
            float x = EditorGroupLayout.XForTime(lane, state.ViewStart, state.ViewSpan, key.Time);
            float distance = Math.Abs(x - point.X);
            if (distance <= best)
            {
                best = distance;
                picked = key.Time;
            }
        }

        if (picked is not { } time)
            return false;

        state.SelectedKey = time;
        state.DragMode = EditorGroupDragMode.Key;
        state.DragRow = -1;
        _dragBefore = _history.Capture();
        _keyDragMoved = false;
        _commands.Enqueue(EditorCommand.StopPlayback());
        _commands.Enqueue(EditorCommand.Seek(Math.Max(0, (long)Math.Round(time * 1000.0))));
        return true;
    }

    /// <summary>
    /// Retimes the dragged key. It never lands on another key of its setting:
    /// it waits at its last free moment and jumps past, so dragging over a key
    /// can't delete it. Letting go records one undo step if it moved.
    /// </summary>
    private void DragKey(
        Entity editor,
        ref EditorGroupState state,
        GroupDefinition group,
        PointerInput input,
        Rect2 lane,
        Playhead playhead)
    {
        if (!input.Pressed)
        {
            if (_keyDragMoved)
                _history.Record(_dragBefore, _history.Capture());
            state.DragMode = EditorGroupDragMode.None;
            return;
        }

        if (!TrySelectedSetting(editor, state, group, out InstanceUniform uniform, out List<ShaderKey> keys))
            return;

        float time = EditorGroupLayout.TimeForX(lane, state.ViewStart, state.ViewSpan, input.Position.X);
        bool snap = state.Snap && !input.Shift;
        float to = EditorGroupLayout.Tidy(snap ? EditorGroupLayout.SnapSeconds(time, playhead) : time);
        if (Math.Abs(to - state.SelectedKey) < EditorShaderKeyEdits.SameMoment ||
            EditorShaderKeyEdits.Find(keys, uniform.Name, to) >= 0)
        {
            return;
        }

        if (EditorShaderKeyEdits.Retime(keys, uniform.Name, state.SelectedKey, to))
        {
            state.SelectedKey = to;
            _keyDragMoved = true;
        }
    }

    /// <summary>The lane's table cells: the setting's name, and the selected key's moment, value and easing.</summary>
    private void ShowKeyLane(
        Entity editor,
        EditorGroupState state,
        GroupDefinition group,
        Vector2 dialog,
        Vector2 origin,
        CommandBuffer buffer)
    {
        Entity name = Piece(EditorGroupPart.KeyName);
        if (state.PaletteOpen)
        {
            Hide(name, buffer);
            foreach (EditorGroupPart part in KeyCells)
                Hide(Piece(part), buffer);
            return;
        }

        Place(name, EditorGroupLayout.Place(EditorGroupPart.KeyName, 0, dialog), origin, buffer);
        if (!TrySelectedSetting(editor, state, group, out InstanceUniform uniform, out List<ShaderKey> keys))
        {
            UiText.Set(name, "NO SHADER", buffer);
            SetTextColor(name, Palette.TextDim, buffer);
            foreach (EditorGroupPart part in KeyCells)
                Hide(Piece(part), buffer);
            return;
        }

        UiText.Set(name, uniform.Name.ToUpperInvariant(), buffer);
        SetTextColor(name, Palette.Accent, buffer);
        if (!HasSelectedKey(state, keys, uniform, out ShaderKey key))
        {
            foreach (EditorGroupPart part in KeyCells)
                Hide(Piece(part), buffer);
            return;
        }

        foreach (EditorGroupPart part in KeyCells)
            Place(Piece(part), EditorGroupLayout.Place(part, 0, dialog), origin, buffer);

        SetNumber(Piece(EditorGroupPart.KeyTime), key.Time, buffer);
        Entity value = Piece(EditorGroupPart.KeyValue);
        if (!value.IsNull && !value.Tags.Has<TextFieldFocused>())
            SetFieldText(value, EditorShaderKeyEdits.Format(key.Value, uniform.Channels), buffer);
        UiText.Set(Piece(EditorGroupPart.KeyEase), EaseLabel(key.Ease), buffer);
        Tint(Piece(EditorGroupPart.KeyEase), ActionIdle, buffer);
        Tint(Piece(EditorGroupPart.KeyDelete), ActionIdle, buffer);
    }

    /// <summary>Typing a key's moment retimes it (replacing a key already there); typing a value rewrites it.</summary>
    private void CommitKeyFields(Entity editor, ref EditorGroupState state, GroupDefinition group, Playhead playhead)
    {
        EditorGroupState current = state;
        bool selected = TrySelectedSetting(editor, state, group, out InstanceUniform uniform, out List<ShaderKey> keys) &&
                        HasSelectedKey(state, keys, uniform, out _);
        float from = state.SelectedKey;
        float? retimed = null;

        CommitIfBlurred(Piece(EditorGroupPart.KeyTime), text =>
        {
            if (!selected || !TryNumber(text, out float typed))
                return;
            float to = Tidy(current, typed, playhead);
            if (Math.Abs(to - from) < EditorShaderKeyEdits.SameMoment)
                return;
            Edit(() => EditorShaderKeyEdits.Retime(keys, uniform.Name, from, to));
            retimed = to;
        });
        CommitIfBlurred(Piece(EditorGroupPart.KeyValue), text =>
        {
            if (!selected || !EditorShaderKeyEdits.TryParse(text, uniform, out Vector4 value))
                return;
            float at = retimed ?? from;
            Edit(() => EditorShaderKeyEdits.Put(keys, uniform, at, value));
        });

        if (retimed is { } moved)
            state.SelectedKey = moved;
    }
}
