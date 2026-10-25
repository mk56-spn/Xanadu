using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Animation;
using Writer.Ecs;
using Writer.Systems.Input;
using Writer.Timeline;
using Writer.Tools.Animator.Commands;

namespace Writer.Tools.Animator.Systems;

/// <summary>
/// The keyframe timeline: scrubbing on the ruler, picking and retiming key
/// sets on track rows, selecting tracks by label, wheel zoom and row scroll.
/// </summary>
public sealed class AnimatorTimelineInputSystem(
    IPointerInputSource pointer,
    IAnimatorWheel wheel) : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private bool _panelHeld;

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
        else if (drag.Kind is AnimatorDragKind.Key or AnimatorDragKind.Scrub)
        {
            if (!input.Pressed)
                End(session, ref drag);
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

    private static void Begin(Entity session, AnimatorTimelineModel model, PointerInput input)
    {
        Vector2 point = input.Position;
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
            AnimatorKeyCommands.SelectTrack(session, model.TrackAt(row));
            return;
        }

        if (model.TryPickKey(point, out XafTrack track, out float time))
        {
            AnimatorHistoryCommands.BeginGesture(session);
            AnimatorKeyCommands.SelectKey(session, track, time);
            Freeze(session, model);
            session.GetComponent<AnimatorDrag>() = new AnimatorDrag { Kind = AnimatorDragKind.Key };
            return;
        }

        AnimatorKeyCommands.SelectTrack(session, model.TrackAt(row));
        BeginScrub(session, model, input);
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

    private static void Drag(
        Entity session,
        AnimatorTimelineModel model,
        AnimatorDragKind kind,
        PointerInput input)
    {
        if (kind == AnimatorDragKind.Key)
        {
            double seconds = model.Snap(model.SecondsAtX(input.Position.X), input.Shift);
            AnimatorKeyCommands.RetimeSelected(session, (float)seconds);
        }
        else
        {
            Scrub(session, model, input);
        }
    }

    private static void Scrub(Entity session, AnimatorTimelineModel model, PointerInput input)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        double seconds = model.Snap(model.SecondsAtX(input.Position.X), input.Shift);
        document.Runtime.Seek((float)seconds);
        document.Runtime.SetPaused(true);
    }

    private static void End(Entity session, ref AnimatorDrag drag)
    {
        if (drag.Kind == AnimatorDragKind.Key)
            AnimatorHistoryCommands.EndGesture(session);

        session.GetComponent<TimelineView>().Scrubbing = false;
        drag = new AnimatorDrag();
    }
}
