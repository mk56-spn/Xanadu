using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Timeline;

namespace Xanadu.Tools.Animator;

/// <summary>
/// Where everything in the keyframe timeline is on screen this frame: the
/// ruler, the track rows, the visible time window and each key. Input and
/// drawing both read it, so they cannot disagree about a key's position.
/// </summary>
public sealed class AnimatorTimelineModel
{
    public const float Padding = 10f;
    public const float LabelWidth = 170f;
    public const float Gap = 8f;
    public const float HeaderHeight = 28f;

    /// <summary>The play and stop buttons at the header's left; the caption starts after them.</summary>
    public const float TransportButtonWidth = 34f;
    public const float TransportWidth = TransportButtonWidth * 2 + 6f + 10f;
    public const float RulerHeight = 24f;
    public const float RowHeight = 20f;
    public const float KeyRadius = 6f;
    public const float KeyHitRadius = 9f;
    public const float LoopHandleRadius = 8f;

    private static readonly HashSet<XafTrack> EmptyExpanded = new();
    public const float MinLabelPixels = 70f;
    public const float MinSnapPixels = 12f;

    private AnimatorTimelineModel(
        Rect2 panel,
        XafClip clip,
        List<AnimatorTrackRow> tracks,
        IReadOnlySet<XafTrack> expanded,
        int firstRow,
        TimeWindow window,
        float zoom)
    {
        Panel = panel;
        Clip = clip;
        Tracks = tracks;
        Expanded = expanded;
        Window = window;
        Zoom = zoom;
        Ruler = RulerOf(panel);
        Rows = RowsOf(panel);
        Labels = new Rect2(panel.Position.X + Padding, Rows.Position.Y, LabelWidth, Rows.Size.Y);
        VisibleRows = Math.Max(1, (int)(Rows.Size.Y / RowHeight));
        FirstRow = Math.Clamp(firstRow, 0, Math.Max(0, tracks.Count - VisibleRows));

        double pixelsPerSecond = Ruler.Size.X / (window.Span / 1000.0);
        LabelStep = TimelineTicks.NiceStepSeconds(pixelsPerSecond, MinLabelPixels);
        SnapStep = TimelineTicks.NiceStepSeconds(pixelsPerSecond, MinSnapPixels);
    }

    public Rect2 Panel { get; }
    public XafClip Clip { get; }
    public IReadOnlyList<AnimatorTrackRow> Tracks { get; }

    /// <summary>Targets whose channel rows are shown.</summary>
    public IReadOnlySet<XafTrack> Expanded { get; }
    public TimeWindow Window { get; }
    public float Zoom { get; }
    public Rect2 Ruler { get; }
    public Rect2 Rows { get; }
    public Rect2 Labels { get; }
    public int VisibleRows { get; }
    public int FirstRow { get; }

    /// <summary>Spacing in seconds of labelled ruler ticks.</summary>
    public double LabelStep { get; }

    /// <summary>Spacing in seconds of minor ticks; keys and the playhead snap to it.</summary>
    public double SnapStep { get; }

    public int LastRow => Math.Min(Tracks.Count, FirstRow + VisibleRows);

    /// <summary>The model for the session's current clip, or null when there is none.</summary>
    public static AnimatorTimelineModel? Create(Entity session)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        XafClip? clip = AnimatorSession.CurrentClip(document);
        if (clip is null || ui.Timeline.IsNull)
            return null;

        Rect2 panel = AnimatorGeometry.PanelArea(ui.Timeline);
        TimelineView view = session.GetComponent<TimelineView>();
        long durationMs = (long)Math.Round(clip.Duration * 1000.0);
        long timeMs = (long)Math.Round(document.Runtime.CurrentTime * 1000.0);
        TimeWindow window = view.Scrubbing
            ? new TimeWindow(view.ScrubStartMs, view.ScrubEndMs)
            : TimeWindow.Following(timeMs, durationMs, view.Zoom);

        XafTrack? selected = SelectedTrack(session, document.Package);
        IReadOnlySet<XafTrack> expanded =
            session.GetComponent<AnimatorTracks>().Expanded ?? EmptyExpanded;
        List<AnimatorTrackRow> tracks = AnimatorTrackRows.Build(
            document.Package, clip, selected, expanded);
        return new AnimatorTimelineModel(
            panel,
            clip,
            tracks,
            expanded,
            session.GetComponent<AnimatorTracks>().FirstRow,
            window,
            view.Zoom);
    }

    /// <summary>The track of the selected bone or attachment, if any.</summary>
    public static XafTrack? SelectedTrack(Entity session, XafPackage package)
    {
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        if (selection.Bone >= 0 && selection.Bone < package.Skeleton.Bones.Count)
            return new XafTrack(XafChannelTarget.Bone, package.Skeleton.Bones[selection.Bone].Id);
        if (selection.Attachment >= 0 && selection.Attachment < package.Attachments.Count)
            return new XafTrack(XafChannelTarget.Attachment, package.Attachments[selection.Attachment].Group);
        return null;
    }

    /// <summary>
    /// Whether a key time falls in the visible window. The window is whole
    /// milliseconds and key times are floats, so a key on the clip's last frame
    /// reads a fraction of a millisecond past the end; a millisecond of slack
    /// keeps it from vanishing.
    /// </summary>
    public bool IsVisible(double seconds)
    {
        double ms = seconds * 1000.0;
        return ms >= Window.Start - 1.0 && ms <= Window.End + 1.0;
    }

    public float XForSeconds(double seconds) => Window.XForTime(Ruler, seconds * 1000.0);

    public double SecondsAtX(float x) => Window.TimeForX(Ruler, x) / 1000.0;

    /// <summary>Rounds a time to the snap step; <paramref name="free"/> leaves it as is.</summary>
    public double Snap(double seconds, bool free) =>
        free || SnapStep <= 0 ? seconds : Math.Round(seconds / SnapStep) * SnapStep;

    public Rect2 RowRect(int screenRow) =>
        new(Rows.Position.X, Rows.Position.Y + screenRow * RowHeight, Rows.Size.X, RowHeight);

    /// <summary>Screen row under a point in the label column or the key area, or -1.</summary>
    public int ScreenRowAt(Vector2 point)
    {
        bool inside = point.X >= Labels.Position.X && point.X <= Rows.End.X &&
                      point.Y >= Rows.Position.Y &&
                      point.Y < Rows.Position.Y + VisibleRows * RowHeight;
        if (!inside)
            return -1;

        int row = (int)((point.Y - Rows.Position.Y) / RowHeight);
        return FirstRow + row < Tracks.Count ? row : -1;
    }

    public AnimatorTrackRow TrackAt(int screenRow) => Tracks[FirstRow + screenRow];

    public bool IsExpanded(XafTrack track) => Expanded.Contains(track);

    /// <summary>Whether the point presses the expand arrow at the start of a target row.</summary>
    public bool IsExpandArrow(Vector2 point, int screenRow)
    {
        if (screenRow < 0 || FirstRow + screenRow >= Tracks.Count)
            return false;

        AnimatorTrackRow row = Tracks[FirstRow + screenRow];
        return !row.IsChannel &&
               point.X >= Labels.Position.X &&
               point.X <= Labels.Position.X + 16f &&
               AnimatorTrackRows.HasChannels(Clip, row.Track);
    }

    /// <summary>The key set nearest the point, within the hit radius, on its row.</summary>
    public bool TryPickKey(Vector2 point, out AnimatorTrackRow rowAt, out float time)
    {
        rowAt = default;
        time = 0f;
        int row = ScreenRowAt(point);
        if (row < 0 || !Rows.HasPoint(point))
            return false;

        rowAt = TrackAt(row);
        float best = KeyHitRadius;
        bool found = false;
        foreach (float candidate in AnimatorTrackRows.KeyTimes(Clip, rowAt))
        {
            float distance = Math.Abs(point.X - XForSeconds(candidate));
            if (distance >= best)
                continue;
            best = distance;
            time = candidate;
            found = true;
        }
        return found;
    }

    /// <summary>
    /// The loop end a press near its handle in the ruler grabs: true for the
    /// start, false for the end. Only while the clip loops.
    /// </summary>
    public bool TryPickLoopHandle(Vector2 point, out bool start)
    {
        start = false;
        if (!Clip.Loop || !Ruler.Grow(4f).HasPoint(point))
            return false;

        float startX = XForSeconds(Clip.LoopStart);
        float endX = XForSeconds(Clip.LoopEnd);
        float toStart = Math.Abs(point.X - startX);
        float toEnd = Math.Abs(point.X - endX);
        if (toStart > LoopHandleRadius && toEnd > LoopHandleRadius)
            return false;

        start = toStart <= toEnd;
        return true;
    }

    /// <summary>Every visible key set whose marker touches the box, in row order.</summary>
    public List<AnimatorKeyRef> KeysIn(Rect2 box)
    {
        var keys = new List<AnimatorKeyRef>();
        for (int screenRow = 0; screenRow < VisibleRows; screenRow++)
        {
            int index = FirstRow + screenRow;
            if (index >= Tracks.Count)
                break;

            Rect2 row = RowRect(screenRow);
            if (!box.Intersects(row, includeBorders: true))
                continue;

            AnimatorTrackRow track = Tracks[index];
            foreach (float time in AnimatorTrackRows.KeyTimes(Clip, track))
            {
                if (!IsVisible(time))
                    continue;

                Vector2 center = new(XForSeconds(time), row.GetCenter().Y);
                var marker = new Rect2(center - new Vector2(KeyRadius, KeyRadius), new Vector2(KeyRadius * 2, KeyRadius * 2));
                if (box.Intersects(marker, includeBorders: true))
                    keys.Add(AnimatorTrackRows.Ref(track, time));
            }
        }

        return keys;
    }

    private static Rect2 RulerOf(Rect2 panel)
    {
        float x = panel.Position.X + Padding + LabelWidth + Gap;
        return new Rect2(
            x,
            panel.Position.Y + Padding + HeaderHeight + 4,
            Math.Max(1f, panel.End.X - Padding - x),
            RulerHeight);
    }

    private static Rect2 RowsOf(Rect2 panel)
    {
        Rect2 ruler = RulerOf(panel);
        float top = ruler.End.Y + 2;
        return new Rect2(
            ruler.Position.X,
            top,
            ruler.Size.X,
            Math.Max(RowHeight, panel.End.Y - Padding - top));
    }
}
