using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Animation;
using Writer.Ecs;
using Writer.Systems.Input;
using Writer.Systems.Ui;
using Writer.Timeline;
using Writer.Tools.Animator.Commands;

namespace Writer.Tools.Animator.Systems;

/// <summary>
/// The keyframe timeline: scrubbing on the ruler, picking and retiming key
/// sets on track rows, dragging a box over empty row space to select the keys
/// it covers, selecting tracks by label, wheel zoom and row scroll.
/// </summary>
public sealed class AnimatorTimelineInputSystem(
    IPointerInputSource pointer,
    IAnimatorWheel wheel) : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private readonly UiMarquee<AnimatorKeyRef> _marquee = new();
    private bool _panelHeld;

    /// <summary>Shift-clicked a selected key: remove it on release unless the press became a drag.</summary>
    private bool _pendingToggle;
    private AnimatorTrackRow _pendingTrack;
    private float _pendingTime;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
            Update(session);
    }

    private void Update(Entity session)
    {
        AnimatorTimelineModel? model = AnimatorTimelineModel.Create(session);
        PointerInput input = pointer.Read();
        Entity panel = session.GetComponent<AnimatorUi>().Timeline;
        bool pressed = !panel.IsNull && panel.Tags.Has<Pressed>();
        bool begin = pressed && !_panelHeld;
        _panelHeld = pressed;

        int notches = wheel.TakeWheel();
        if (model is null)
            return;

        if (notches != 0 && model.Panel.HasPoint(input.Position))
            Wheel(session, model, input.Position, notches);

        ref AnimatorDrag drag = ref session.GetComponent<AnimatorDrag>();
        if (begin && model.Panel.HasPoint(input.Position))
        {
            Begin(session, model, input);
        }
        else if (drag.Kind is AnimatorDragKind.Key or AnimatorDragKind.Scrub or
                 AnimatorDragKind.KeyBox or AnimatorDragKind.LoopStart or
                 AnimatorDragKind.LoopEnd)
        {
            if (!input.Pressed)
                End(session, ref drag, input.Position);
            else
                Drag(session, model, drag.Kind, input);
        }
    }

    private void Wheel(Entity session, AnimatorTimelineModel model, Vector2 point, int notches)
    {
        if (wheel.Control || model.Ruler.HasPoint(point))
        {
            ref TimelineView view = ref session.GetComponent<TimelineView>();
            view.Zoom = TimelineZoom.Step(view.Zoom, notches > 0 ? 1 : -1);
            return;
        }

        int max = Math.Max(0, model.Tracks.Count - model.VisibleRows);
        ref AnimatorTracks tracks = ref session.GetComponent<AnimatorTracks>();
        tracks.FirstRow = Math.Clamp(model.FirstRow - Math.Sign(notches), 0, max);
    }

    private void Begin(Entity session, AnimatorTimelineModel model, PointerInput input)
    {
        Vector2 point = input.Position;
        if (model.TryPickLoopHandle(point, out bool loopStart))
        {
            BeginLoop(session, loopStart);
            return;
        }

        if (model.Ruler.HasPoint(point))
        {
            BeginScrub(session, model, input);
            return;
        }

        int row = model.ScreenRowAt(point);
        if (row < 0)
            return;

        if (!model.Rows.HasPoint(point))
        {
            AnimatorTrackRow rowAt = model.TrackAt(row);
            if (model.IsExpandArrow(point, row))
                ToggleExpanded(session, rowAt);
            else
                AnimatorKeyCommands.SelectTrack(session, rowAt);
            return;
        }

        if (model.TryPickKey(point, out AnimatorTrackRow track, out float time))
        {
            // Shift-click adds the key, or takes a selected one out once the
            // press is released without a drag. A Shift-drag still retimes
            // freely, so the existing "Shift = free" behaviour is kept.
            _pendingToggle = false;
            if (input.Shift)
            {
                if (AnimatorKeyCommands.IsSelected(session, track, time))
                {
                    _pendingToggle = true;
                    _pendingTrack = track;
                    _pendingTime = time;
                }
                else
                {
                    AnimatorKeyCommands.ToggleKey(session, track, time);
                }

                AnimatorHistoryCommands.BeginGesture(session);
                Freeze(session, model);
                session.GetComponent<AnimatorDrag>() = new AnimatorDrag
                {
                    Kind = AnimatorDragKind.Key,
                    StartPointer = point,
                };
                return;
            }

            AnimatorHistoryCommands.BeginGesture(session);

            // A key in a selected group keeps the group, so dragging it moves them all.
            if (AnimatorKeyCommands.IsSelected(session, track, time))
                AnimatorKeyCommands.MakePrimary(session, track, time);
            else
                AnimatorKeyCommands.SelectKey(session, track, time);
            Freeze(session, model);
            session.GetComponent<AnimatorDrag>() = new AnimatorDrag { Kind = AnimatorDragKind.Key };
            return;
        }

        BeginBox(session, model, input, row);
    }

    /// <summary>
    /// A press on empty row space selects the row's track and puts the playhead
    /// there, as a click always has, and arms a box: dragging on selects the
    /// keys it covers. Shift keeps the keys already selected and adds to them.
    /// </summary>
    private void BeginBox(Entity session, AnimatorTimelineModel model, PointerInput input, int row)
    {
        List<AnimatorKeyRef> existing = session.GetComponent<AnimatorSelection>().Keys();
        AnimatorKeyCommands.SelectTrack(session, model.TrackAt(row));
        Freeze(session, model);
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag { Kind = AnimatorDragKind.KeyBox };

        if (input.Shift)
        {
            // SelectTrack leaves the keys alone, so the selection is still there.
            _marquee.Begin(input.Position, additive: true, existing);
            return;
        }

        session.GetComponent<AnimatorSelection>().HasKey = false;
        Scrub(session, model, input);
        _marquee.Arm(input.Position);
    }

    /// <summary>Shows or hides a target's per-channel rows, and selects it as well.</summary>
    private static void ToggleExpanded(Entity session, AnimatorTrackRow row)
    {
        ref AnimatorTracks tracks = ref session.GetComponent<AnimatorTracks>();
        HashSet<XafTrack> expanded = tracks.Expanded ??= new HashSet<XafTrack>();
        bool open;
        if (expanded.Contains(row.Track))
        {
            expanded.Remove(row.Track);
            open = false;
        }
        else
        {
            expanded.Add(row.Track);
            open = true;
        }

        if (!open)
        {
            // Keys on the channel rows that just vanished would stay selected
            // invisibly, so drop them with the rows.
            ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
            if (selection.Keys().Exists(key => key.Target == row.Target && key.Id == row.Id))
                selection.HasKey = false;
        }

        AnimatorKeyCommands.SelectTrack(session, row);
        AnimatorSession.SetStatus(
            session,
            $"{(open ? "EXPANDED" : "COLLAPSED")} {row.Id}");
    }

    /// <summary>Starts dragging one end of the loop range; the whole drag is one undo step.</summary>
    private static void BeginLoop(Entity session, bool start)
    {
        AnimatorHistoryCommands.BeginGesture(session);
        session.GetComponent<AnimatorDocument>().Runtime.SetPaused(true);
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag
        {
            Kind = start ? AnimatorDragKind.LoopStart : AnimatorDragKind.LoopEnd,
        };
        AnimatorSession.SetStatus(session, start ? "LOOP START" : "LOOP END");
    }

    private static void BeginScrub(Entity session, AnimatorTimelineModel model, PointerInput input)
    {
        session.GetComponent<AnimatorSelection>().HasKey = false;
        Freeze(session, model);
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag { Kind = AnimatorDragKind.Scrub };
        Scrub(session, model, input);
    }

    /// <summary>Keeps the visible window still while a drag moves the playhead.</summary>
    private static void Freeze(Entity session, AnimatorTimelineModel model)
    {
        ref TimelineView view = ref session.GetComponent<TimelineView>();
        view.Scrubbing = true;
        view.ScrubStartMs = model.Window.Start;
        view.ScrubEndMs = model.Window.End;
    }

    private void Drag(
        Entity session,
        AnimatorTimelineModel model,
        AnimatorDragKind kind,
        PointerInput input)
    {
        if (kind == AnimatorDragKind.KeyBox)
        {
            DragBox(session, model, input);
            return;
        }

        if (kind is AnimatorDragKind.LoopStart or AnimatorDragKind.LoopEnd)
        {
            DragLoop(session, model, kind, input);
        }
        else if (kind == AnimatorDragKind.Key)
        {
            double seconds = model.Snap(model.SecondsAtX(input.Position.X), input.Shift);
            AnimatorKeyCommands.RetimeSelected(session, (float)seconds);
        }
        else
        {
            Scrub(session, model, input);
        }
    }

    /// <summary>Follows the pointer and selects the keys the box covers, replacing or adding to the selection.</summary>
    private void DragBox(Entity session, AnimatorTimelineModel model, PointerInput input)
    {
        if (!_marquee.Update(input.Position, out bool started))
            return;

        if (started)
            session.GetComponent<AnimatorSelection>().HasKey = false;

        _marquee.Publish(ref session.GetComponent<UiMarqueeBox>());
        AnimatorKeyCommands.SelectKeys(session, _marquee.Selection(model.KeysIn(_marquee.Bounds)));
    }

    /// <summary>Moves one loop end to the pointer, snapped, and reports the new range.</summary>
    private static void DragLoop(
        Entity session,
        AnimatorTimelineModel model,
        AnimatorDragKind kind,
        PointerInput input)
    {
        float seconds = (float)model.Snap(model.SecondsAtX(input.Position.X), input.Shift);
        if (kind == AnimatorDragKind.LoopStart)
            XafClipAuthoring.SetLoopStart(model.Clip, seconds);
        else
            XafClipAuthoring.SetLoopEnd(model.Clip, seconds);
        AnimatorSession.SetStatus(
            session,
            $"LOOP {model.Clip.LoopStart:0.00}–{model.Clip.LoopEnd:0.00}s");
    }

    private static void Scrub(Entity session, AnimatorTimelineModel model, PointerInput input)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        double seconds = model.Snap(model.SecondsAtX(input.Position.X), input.Shift);
        document.Runtime.Seek((float)seconds);
        document.Runtime.SetPaused(true);
    }

    private void End(Entity session, ref AnimatorDrag drag, Vector2 pointer)
    {
        if (drag.Kind == AnimatorDragKind.Key)
        {
            // A Shift-press that did not move is a click on an already selected
            // key, which takes it out of the selection.
            if (_pendingToggle && pointer.DistanceTo(drag.StartPointer) < 3f)
                AnimatorKeyCommands.ToggleKey(session, _pendingTrack, _pendingTime);
            AnimatorHistoryCommands.EndGesture(session);
        }

        if (drag.Kind is AnimatorDragKind.LoopStart or AnimatorDragKind.LoopEnd)
        {
            XafClip? clip = AnimatorSession.CurrentClip(
                session.GetComponent<AnimatorDocument>());
            if (clip is not null)
            {
                AnimatorSession.SetStatus(
                    session,
                    $"LOOP {clip.LoopStart:0.00}–{clip.LoopEnd:0.00}s");
            }
            AnimatorHistoryCommands.EndGesture(session);
        }

        if (drag.Kind == AnimatorDragKind.KeyBox)
        {
            _marquee.End();
            _marquee.Publish(ref session.GetComponent<UiMarqueeBox>());
        }

        _pendingToggle = false;
        session.GetComponent<TimelineView>().Scrubbing = false;
        drag = new AnimatorDrag();
    }
}
