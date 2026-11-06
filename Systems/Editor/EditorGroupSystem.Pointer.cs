using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Editor;

/// <summary>Dragging an effect's bar and its ends on the group timeline.</summary>
public sealed partial class EditorGroupSystem
{
    // ------------------------------------------------------------------ pointer

    /// <summary>Dragging an effect's bar on the timeline: the whole bar, or one of its ends.</summary>
    private void HandlePointer(
        Entity editor,
        ref EditorGroupState state,
        GroupDefinition group,
        ref Playhead playhead,
        Vector2 dialog,
        Vector2 origin)
    {
        PointerInput input = _pointer.Read();
        bool press = input.Pressed && !_pointerHeld;
        _pointerHeld = input.Pressed;
        if (state.PaletteOpen)
            return;

        Rect2 timeline = Offset(EditorGroupLayout.Timeline(dialog), origin);
        Rect2 ruler = Offset(EditorGroupLayout.Ruler(dialog), origin);
        Rect2 lane = Offset(EditorGroupLayout.KeyLane(dialog), origin);

        if (press)
        {
            if (BeginKeyDrag(editor, ref state, group, input.Position, lane))
                return;

            if (!BeginDrag(ref state, group, input.Position, timeline) &&
                (timeline.HasPoint(input.Position) || ruler.HasPoint(input.Position) || lane.HasPoint(input.Position)))
            {
                state.DragMode = EditorGroupDragMode.Scrub;
                state.DragRow = -1;
                _commands.Enqueue(EditorCommand.StopPlayback());
                SeekTo(timeline, state, input.Position.X);
                return;
            }
        }

        if (state.DragMode == EditorGroupDragMode.Key)
        {
            DragKey(editor, ref state, group, input, lane, playhead);
            return;
        }

        if (state.DragMode == EditorGroupDragMode.Scrub)
        {
            if (!input.Pressed)
            {
                state.DragMode = EditorGroupDragMode.None;
                return;
            }

            SeekTo(timeline, state, input.Position.X);
            return;
        }

        if (state.DragRow < 0 || state.DragRow >= group.Effects.Count)
        {
            state.DragRow = -1;
            return;
        }

        if (!input.Pressed)
        {
            _history.Record(_dragBefore, _history.Capture());
            state.DragRow = -1;
            state.DragMode = EditorGroupDragMode.None;
            return;
        }

        float time = EditorGroupLayout.TimeForX(timeline, state.ViewStart, state.ViewSpan, input.Position.X);
        GroupEffect effect = group.Effects[state.DragRow];
        bool snap = state.Snap && !input.Shift;
        Playhead timing = playhead;
        float Snapped(float t) => EditorGroupLayout.Tidy(snap ? EditorGroupLayout.SnapSeconds(t, timing) : t);

        switch (state.DragMode)
        {
            case EditorGroupDragMode.Whole:
                float length = effect.End - effect.Start;
                float start = Snapped(time - state.DragGrab);
                group.Effects[state.DragRow] = effect with { Start = start, End = EditorGroupLayout.Tidy(start + length) };
                break;
            case EditorGroupDragMode.Start:
                group.Effects[state.DragRow] = effect with { Start = Math.Min(Snapped(time), effect.End) };
                break;
            case EditorGroupDragMode.End:
                group.Effects[state.DragRow] = effect with { End = Math.Max(Snapped(time), effect.Start) };
                break;
        }
    }

    private bool BeginDrag(ref EditorGroupState state, GroupDefinition group, Vector2 point, Rect2 timeline)
    {
        for (int row = 0; row < group.Effects.Count && row < EditorGroupLayout.MaxRows; row++)
        {
            Rect2 bar = BarRect(group.Effects[row], row, state, timeline);
            if (!bar.Grow(2f).HasPoint(point))
                continue;

            state.SelectedGroup = state.Open;
            state.SelectedEffect = row;
            _dragBefore = _history.Capture();
            state.DragRow = row;
            if (point.X <= bar.Position.X + EdgeGrab && bar.Size.X > EdgeGrab * 3)
                state.DragMode = EditorGroupDragMode.Start;
            else if (point.X >= bar.End.X - EdgeGrab && bar.Size.X > EdgeGrab * 3)
                state.DragMode = EditorGroupDragMode.End;
            else
                state.DragMode = EditorGroupDragMode.Whole;

            float time = EditorGroupLayout.TimeForX(timeline, state.ViewStart, state.ViewSpan, point.X);
            state.DragGrab = time - group.Effects[row].Start;
            return true;
        }

        return false;
    }

    private void SeekTo(Rect2 timeline, EditorGroupState state, float x)
    {
        float seconds = EditorGroupLayout.TimeForX(
            timeline, state.ViewStart, state.ViewSpan, x);
        _commands.Enqueue(EditorCommand.Seek(
            Math.Max(0, (long)Math.Round(seconds * 1000.0))));
    }

    private static Rect2 BarRect(GroupEffect effect, int row, EditorGroupState state, Rect2 timeline) =>
        EditorGroupLayout.BarRect(effect, row, state.ViewStart, state.ViewSpan, timeline);

    private static Rect2 Offset(Rect2 rect, Vector2 by) => new(rect.Position + by, rect.Size);
}
