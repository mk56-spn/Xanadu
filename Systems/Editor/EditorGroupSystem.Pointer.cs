using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Systems.Input;
using Xanadu.Timeline;

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

        // A press on the ease picker is the picker's, not the timeline's under it.
        if (press && EasePickerArea(editor, state).HasPoint(input.Position))
            press = false;

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

        TimelineAxis axis = EditorGroupLayout.Axis(timeline, state.ViewStart, state.ViewSpan);
        GroupEffect effect = group.Effects[state.DragRow];
        bool snap = state.Snap && !input.Shift;
        Playhead timing = playhead;
        double Snapped(double ms) =>
            EditorGroupLayout.Tidy(snap ? EditorGroupLayout.SnapSeconds((float)(ms / 1000.0), timing) : (float)(ms / 1000.0)) * 1000.0;

        (double from, double to) = _effectDrag.Apply(axis.TimeAt(input.Position.X), Snapped);
        group.Effects[state.DragRow] = effect with
        {
            Start = EditorGroupLayout.Tidy((float)(from / 1000.0)),
            End = EditorGroupLayout.Tidy((float)(to / 1000.0)),
        };
    }

    /// <summary>
    /// A press on an effect's bar: near an end it retimes that end, elsewhere it
    /// moves the effect whole, picked the way the animator picks an override.
    /// </summary>
    private bool BeginDrag(ref EditorGroupState state, GroupDefinition group, Vector2 point, Rect2 timeline)
    {
        TimelineAxis axis = EditorGroupLayout.Axis(timeline, state.ViewStart, state.ViewSpan);
        for (int row = 0; row < group.Effects.Count && row < EditorGroupLayout.MaxRows; row++)
        {
            GroupEffect effect = group.Effects[row];
            Rect2 band = EditorGroupLayout.RowRect(row, timeline);
            if (point.Y < band.Position.Y + 2f || point.Y > band.End.Y - 2f ||
                TimelineGrab.Span(axis, effect.Start * 1000.0, effect.End * 1000.0, point.X, EdgeGrab, EdgeGrab * 3)
                    is not { } part)
            {
                continue;
            }

            state.SelectedGroup = state.Open;
            state.SelectedEffect = row;
            _dragBefore = _history.Capture();
            state.DragRow = row;
            state.DragMode = part switch
            {
                TimelineGrabPart.Start => EditorGroupDragMode.Start,
                TimelineGrabPart.End => EditorGroupDragMode.End,
                _ => EditorGroupDragMode.Whole,
            };

            double grabbed = axis.TimeAt(point.X);
            state.DragGrab = (float)(grabbed / 1000.0) - effect.Start;
            _effectDrag = new TimelineSpanDrag(part, effect.Start * 1000.0, effect.End * 1000.0, grabbed);
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


    private static Rect2 Offset(Rect2 rect, Vector2 by) => new(rect.Position + by, rect.Size);
}
