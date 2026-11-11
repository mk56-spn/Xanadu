using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Timeline;
using Xanadu.Timeline;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tools.Animator;

/// <summary>
/// The animator's clip as a shared timeline. Its markers are the keys; on the
/// ruler sit the event flags and loop ends, and the rows carry key sets and
/// override bars. The shared <see cref="TimelineInputSystem"/> reads presses
/// and drags; this says what they do to the clip.
/// </summary>
public sealed class AnimatorTimelineHost(Entity session) : ITimelineHost, ITimelineInput
{
    private const int EventGroup = 0;
    private const int LoopGroup = 1;
    private const int OverrideGroup = 2;
    private const int KeyGroup = 3;

    /// <summary>How far a press on an event flag or an override bar moves before it retimes, so a pick does not snap it.</summary>
    private const float PickSlop = 3f;

    /// <summary>An event's flag on the ruler, by its index in the clip.</summary>
    private sealed record EventFlag(int Index);

    /// <summary>One end of the loop range on the ruler.</summary>
    private sealed record LoopEnd(bool Start);

    /// <summary>What a key drag started from, so each move is measured from there.</summary>
    private AnimatorKeyCommands.KeyDragOrigin? _keyDrag;

    private AnimatorOverrideDrag? _overrideDrag;

    private AnimatorRuntime Runtime => session.GetComponent<AnimatorDocument>().Runtime;

    public double NowMs => Runtime.CurrentTime * 1000.0;

    public double DurationMs => Runtime.CurrentDuration * 1000.0;

    public double StepMs =>
        (AnimatorTimelineModel.Create(session)?.SnapStep ?? AnimatorShortcutSystem.DefaultStep) * 1000.0;

    public double StepOriginMs => 0;

    public bool Blocked => session.GetComponent<AnimatorNewClipDialog>().Open;

    public bool TryGetArea(out Rect2 panel, out Rect2 ruler)
    {
        AnimatorTimelineModel? model = AnimatorTimelineModel.Create(session);
        panel = model?.Panel ?? default;
        ruler = model?.Ruler ?? default;
        return model is not null;
    }

    public IEnumerable<double> MarkerTimesMs()
    {
        XafClip? clip = AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
        if (clip is null)
            yield break;

        foreach (XafChannel channel in clip.Channels)
        {
            foreach (XafKeyframe key in channel.Keyframes)
                yield return key.Time * 1000.0;
        }
    }

    public void SeekMs(double ms)
    {
        float seconds = (float)(ms / 1000.0);
        AnimatorRuntime runtime = Runtime;
        runtime.Seek(seconds);
        runtime.SetPaused(true);
        session.GetComponent<AnimatorSelection>().HasKey = false;
        AnimatorSession.SetStatus(session, $"{seconds:0.00}s");
    }

    public void TogglePlay() =>
        AnimatorCommands.Run(session, new ToolAction(ToolActionKind.TogglePlay));

    public void NoMarker(int direction) =>
        AnimatorSession.SetStatus(session, direction < 0 ? "NO EARLIER KEY" : "NO LATER KEY");

    public TimelineScene? Scene()
    {
        AnimatorTimelineModel? model = AnimatorTimelineModel.Create(session);
        if (model is null)
            return null;

        XafClip clip = model.Clip;
        var scene = new TimelineScene
        {
            Surface = session.GetComponent<AnimatorUi>().Timeline,
            Panel = model.Panel,
            Axis = model.Axis,
            ScrubBand = model.RulerBand,
            DurationMs = clip.Duration * 1000.0,
            Snap = ms => model.Snap(ms / 1000.0, false) * 1000.0,
            Lanes = new TimelineLanes(
                model.Rows,
                model.Labels,
                AnimatorTimelineModel.RowHeight,
                model.Tracks.Count,
                model.FirstRow,
                model.VisibleRows,
                model.RowBar,
                model.RowBarHit,
                AnimatorTimelineModel.MinRowThumb),
            Selection = session.GetComponent<AnimatorSelection>().Keys().ConvertAll(key => (object)key),
        };

        Rect2 flags = model.Ruler.Grow(4f);
        for (int i = 0; i < clip.Events.Count; i++)
        {
            scene.Items.Add(TimelineItem.Marker(new EventFlag(i), EventGroup, clip.Events[i].Time * 1000.0, area: flags) with
            {
                Reach = AnimatorTimelineModel.EventHitRadius,
                Role = TimelineItemRole.Handle,
                Slop = PickSlop,
            });
        }

        if (clip.Loop)
        {
            // Only the ruler's lower half, where the tabs are, so the upper half always scrubs.
            float top = model.Ruler.End.Y - AnimatorTimelineModel.LoopTabHeight - 2f;
            var tabs = new Rect2(flags.Position.X, top, flags.Size.X, flags.End.Y - top);

            // The start comes last, so it wins when both ends are as near.
            scene.Items.Add(LoopItem(false, clip.LoopEnd, tabs));
            scene.Items.Add(LoopItem(true, clip.LoopStart, tabs));
        }

        for (int lane = 0; lane < model.Tracks.Count; lane++)
            AddRow(scene, model, lane);

        return scene;
    }

    private static TimelineItem LoopItem(bool start, float seconds, Rect2 tabs) =>
        TimelineItem.Marker(new LoopEnd(start), LoopGroup, seconds * 1000.0, area: tabs) with
        {
            Reach = AnimatorTimelineModel.LoopHandleRadius,
            Role = TimelineItemRole.Handle,
        };

    /// <summary>A row's override bars, or its key sets.</summary>
    private void AddRow(TimelineScene scene, AnimatorTimelineModel model, int lane)
    {
        AnimatorTrackRow row = model.Tracks[lane];
        XafClip clip = model.Clip;
        if (row.IsOverride)
        {
            foreach (XafLimbTarget target in clip.Targets)
            {
                if (target.BoneId != row.Id)
                    continue;

                (float low, float high) = AnimatorOverrideCommands.Room(clip, target);
                scene.Items.Add(TimelineItem.Span(target, OverrideGroup, target.From * 1000.0, target.To * 1000.0, lane) with
                {
                    Reach = AnimatorTimelineModel.OverrideGripRadius,
                    Slop = PickSlop,
                    LowMs = low * 1000.0,
                    HighMs = high * 1000.0,
                    MinLengthMs = AnimatorOverrideCommands.MinDuration * 1000.0,
                });
            }
            return;
        }

        foreach (float time in AnimatorTrackRows.KeyTimes(clip, row))
        {
            scene.Items.Add(TimelineItem.Marker(AnimatorTrackRows.Ref(row, time), KeyGroup, time * 1000.0, lane) with
            {
                Reach = AnimatorTimelineModel.KeyHitRadius,
                Size = AnimatorTimelineModel.KeyRadius,
                Selected = AnimatorKeyCommands.IsSelected(session, row, time),
            });
        }
    }

    public void GestureStarted(TimelineGestureKind kind)
    {
        ref AnimatorDrag drag = ref session.GetComponent<AnimatorDrag>();
        switch (kind)
        {
            case TimelineGestureKind.Scrub:
                session.GetComponent<AnimatorSelection>().HasKey = false;
                drag = new AnimatorDrag { Kind = AnimatorDragKind.Scrub };
                break;
            case TimelineGestureKind.RowScroll:
                drag = new AnimatorDrag { Kind = AnimatorDragKind.RowScroll };
                break;
            case TimelineGestureKind.Box:
                drag = new AnimatorDrag { Kind = AnimatorDragKind.KeyBox };
                break;
        }
    }

    public void GestureEnded(TimelineGestureKind kind)
    {
        _keyDrag = null;
        _overrideDrag = null;
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag();
    }

    public void ScrollRows(int first) => session.GetComponent<AnimatorTracks>().FirstRow = first;

    public void Scrub(double ms)
    {
        AnimatorRuntime runtime = Runtime;
        runtime.Seek((float)(ms / 1000.0));
        runtime.SetPaused(true);
    }

    /// <summary>The expand arrow shows or hides a target's channel rows; anywhere else on the label selects the track.</summary>
    public void LabelPressed(int lane, Vector2 point)
    {
        if (AnimatorTimelineModel.Create(session) is not { } model || lane >= model.Tracks.Count)
            return;

        AnimatorTrackRow row = model.Tracks[lane];
        if (model.IsExpandArrow(point, lane - model.FirstRow))
            ToggleExpanded(row);
        else
            AnimatorKeyCommands.SelectTrack(session, row);
    }

    /// <summary>
    /// Empty row space selects the row's track and, unless a Shift box is adding
    /// to the keys, drops the key selection and puts the playhead there.
    /// </summary>
    public void LanePressed(int lane, double ms, bool additive)
    {
        if (AnimatorTimelineModel.Create(session) is not { } model || lane >= model.Tracks.Count)
            return;

        // SelectTrack leaves the keys alone, so a Shift box still has them.
        AnimatorKeyCommands.SelectTrack(session, model.Tracks[lane]);
        if (additive)
            return;

        session.GetComponent<AnimatorSelection>().HasKey = false;
        Scrub(ms);
    }

    public void ItemPressed(in TimelineItem item, TimelinePick pick, TimelineGrabPart part)
    {
        switch (item.Ref)
        {
            case EventFlag flag when AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>()) is { } clip &&
                                     flag.Index < clip.Events.Count:
                AnimatorEventCommands.Select(session, clip.Id, flag.Index);
                AnimatorHistoryCommands.BeginGesture(session);
                session.GetComponent<AnimatorDrag>() = new AnimatorDrag { Kind = AnimatorDragKind.Event };
                AnimatorSession.SetStatus(session,
                    $"EVENT {clip.Events[flag.Index].Name.ToUpperInvariant()} · DRAG TO MOVE IT");
                break;

            case LoopEnd end:
                AnimatorHistoryCommands.BeginGesture(session);
                Runtime.SetPaused(true);
                session.GetComponent<AnimatorDrag>() = new AnimatorDrag
                {
                    Kind = end.Start ? AnimatorDragKind.LoopStart : AnimatorDragKind.LoopEnd,
                };
                AnimatorSession.SetStatus(session, end.Start ? "LOOP START" : "LOOP END");
                break;

            case XafLimbTarget target:
                _overrideDrag = AnimatorOverrideCommands.Begin(session, target);
                break;

            case AnimatorKeyRef key:
                PressKey(key, pick);
                break;
        }
    }

    /// <summary>
    /// Shift adds the key, or marks a selected one to come out on a click; a
    /// plain press keeps a selected key's group, so dragging it moves them all.
    /// Either way the drag that may follow is one undo step.
    /// </summary>
    private void PressKey(AnimatorKeyRef key, TimelinePick pick)
    {
        var row = new AnimatorTrackRow(key.Track, key.Property);
        if (pick == TimelinePick.Add)
            AnimatorKeyCommands.ToggleKey(session, row, key.Time);

        AnimatorHistoryCommands.BeginGesture(session);
        if (pick == TimelinePick.Lead)
            AnimatorKeyCommands.MakePrimary(session, row, key.Time);
        else if (pick == TimelinePick.Only)
            AnimatorKeyCommands.SelectKey(session, row, key.Time);

        session.GetComponent<AnimatorDrag>() = new AnimatorDrag { Kind = AnimatorDragKind.Key };
    }

    public void ItemMoved(in TimelineItem item, double fromMs, double toMs)
    {
        switch (item.Ref)
        {
            case EventFlag:
                AnimatorEventCommands.Retime(session, (float)(fromMs / 1000.0));
                break;

            case LoopEnd end when AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>()) is { } clip:
                if (end.Start)
                    XafClipAuthoring.SetLoopStart(clip, (float)(fromMs / 1000.0));
                else
                    XafClipAuthoring.SetLoopEnd(clip, (float)(fromMs / 1000.0));
                ReportLoop(clip);
                break;

            case XafLimbTarget when _overrideDrag is { } dragged:
                AnimatorOverrideCommands.Drag(session, dragged, fromMs / 1000.0, toMs / 1000.0);
                break;

            case AnimatorKeyRef:
                _keyDrag ??= AnimatorKeyCommands.BeginKeyDrag(session);
                AnimatorKeyCommands.RetimeSelected(session, (float)(fromMs / 1000.0), _keyDrag);
                break;
        }
    }

    public void ItemReleased(in TimelineItem item, bool remove)
    {
        XafClip? clip = AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
        switch (item.Ref)
        {
            case AnimatorKeyRef key:
                if (remove)
                    AnimatorKeyCommands.ToggleKey(session, new AnimatorTrackRow(key.Track, key.Property), key.Time);
                AnimatorHistoryCommands.EndGesture(session);
                break;

            case EventFlag:
                AnimatorHistoryCommands.EndGesture(session);
                break;

            case XafLimbTarget when _overrideDrag is { } dragged && clip is not null:
                AnimatorOverrideCommands.End(session, clip, dragged);
                break;

            case LoopEnd:
                if (clip is not null)
                    ReportLoop(clip);
                AnimatorHistoryCommands.EndGesture(session);
                break;
        }
    }

    /// <summary>A box replaces the key selection with what it covers, and drops the primary key with it.</summary>
    public void BoxSelected(IReadOnlyList<object> selection)
    {
        var keys = new List<AnimatorKeyRef>(selection.Count);
        foreach (object item in selection)
        {
            if (item is AnimatorKeyRef key)
                keys.Add(key);
        }

        session.GetComponent<AnimatorSelection>().HasKey = false;
        AnimatorKeyCommands.SelectKeys(session, keys);
    }

    private void ReportLoop(XafClip clip) =>
        AnimatorSession.SetStatus(session, $"LOOP {clip.LoopStart:0.00}–{clip.LoopEnd:0.00}s");

    /// <summary>Shows or hides a target's per-channel rows, and selects it as well.</summary>
    private void ToggleExpanded(AnimatorTrackRow row)
    {
        ref AnimatorTracks tracks = ref session.GetComponent<AnimatorTracks>();
        HashSet<XafTrack> expanded = tracks.Expanded ??= new HashSet<XafTrack>();
        bool open = expanded.Add(row.Track);
        if (!open)
        {
            expanded.Remove(row.Track);

            // Keys on the channel rows that just vanished would stay selected
            // invisibly, so drop them with the rows.
            ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
            if (selection.Keys().Exists(key => key.Target == row.Target && key.Id == row.Id))
                selection.HasKey = false;
        }

        AnimatorKeyCommands.SelectTrack(session, row);
        AnimatorSession.SetStatus(session, $"{(open ? "EXPANDED" : "COLLAPSED")} {row.Id}");
    }
}
