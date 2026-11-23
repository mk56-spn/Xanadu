using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Core.Timeline;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Timeline;

namespace Xanadu.Drawers.Animator;

/// <summary>Draws the keyframe timeline: ruler, track rows, key diamonds and the playhead.</summary>
public sealed class AnimatorTimelineDrawSystem(IRectCanvas canvas, Handle target)
    : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private static readonly Color RulerBackground = Palette.SurfaceInset;
    private static readonly Color RowEven = Palette.SurfaceDeep;
    private static readonly Color RowOdd = new("#171717");
    private static readonly Color RowSelected = new("#164e63");
    private static readonly Color RowSelectedExtra = new("#115e59");
    private static readonly Color RowSelectedChannel = Palette.SelectedDeep;
    private static readonly Color Grid = Palette.SurfaceAlt;
    private static readonly Color Tick = Palette.BorderSubtle;
    private static readonly Color Dim = Palette.TextDim;
    private static readonly Color RestNote = Palette.Warning;

    /// <summary>A field the full view lists that has no keys yet.</summary>
    private static readonly Color Unkeyed = new(Palette.TextDim, 0.45f);
    private static readonly Color Text = Palette.Text;
    private static readonly Color KeyColor = Palette.Info;
    private static readonly Color DrivenBand = new(Palette.Warning, 0.16f);
    private RigPackage? _package;
    private RigLimbTarget? _selectedOverride;
    private static readonly Color EaseLineQuiet = new(Palette.TextDim, 0.45f);
    private static readonly Color EaseReverse = Palette.Danger;
    private static readonly Color Playhead = Palette.Accent;
    private static readonly Color LoopBand = new("#2dd4bf", 0.18f);
    private static readonly Color LoopLine = new("#2dd4bf");
    private static readonly Color EventColor = Palette.Lime;
    private static readonly Color EventSelected = Palette.Warning;
    private static readonly Color EventLine = new(Palette.Lime, 0.25f);

    protected override void OnUpdate()
    {
        canvas.Clear(target);
        foreach (Entity session in Query.ToEntityList())
        {
            // EDIT RIG: the rest pose has no keys, so a note takes the rows' place
            // (AnimatorTimelineHeaderSystem hides the bar's controls).
            if (AnimatorModes.Of(session).Rest)
            {
                DrawRestNote(session.GetComponent<AnimatorUi>());
                continue;
            }

            AnimatorTimelineModel? model = AnimatorTimelineModel.Create(session);
            if (model is not null)
                Draw(session, model);
        }
    }

    private void DrawRestNote(AnimatorUi ui)
    {
        if (ui.Timeline.IsNull || !ui.Timeline.Enabled)
            return;
        Rect2 panel = AnimatorGeometry.PanelArea(ui.Timeline);
        canvas.DrawTextStyledLeft(
            target,
            new Rect2(panel.Position.X + 24, panel.Position.Y + panel.Size.Y / 2 - 10, panel.Size.X - 48, 20),
            "EDITING THE RIG: the rest pose has no keyframes. Leave EDIT RIG to animate.",
            RestNote,
            14);
    }

    private void Draw(Entity session, AnimatorTimelineModel model)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        RigTrack? selected = AnimatorTimelineModel.SelectedTrack(session, document.Package);

        DrawRuler(model);
        _package = document.Package;
        _selectedOverride = AnimatorSelected.Target(session);
        DrawRows(model, selection, selected, document.Package);
        DrawLoop(model);
        DrawPlayhead(model, document.Runtime.CurrentTime);
        DrawEvents(model, AnimatorSelected.Event(session, out _));
        DrawRowBar(model, session.GetComponent<AnimatorDrag>().Kind == AnimatorDragKind.RowScroll);
        DrawTimeBar(model, session.GetComponent<AnimatorDrag>().Kind == AnimatorDragKind.TimeScroll);

        // The box being dragged to select keys, over everything else.
        if (session.TryGetComponent(out UiMarqueeBox box) && box.Active &&
            model.BoxOnScreen(box.Bounds) is { } onScreen)
        {
            CanvasMarquee.Draw(canvas, target, onScreen);
        }
    }

    /// <summary>The scrollbar beside the rows, only when some of them are out of sight.</summary>
    private void DrawRowBar(AnimatorTimelineModel model, bool dragging)
    {
        if (!model.HasRowBar)
            return;

        Rect2 bar = model.RowBar;
        ScrollSlider slider = model.RowSlider;
        canvas.DrawRect(target, bar, RowEven);
        canvas.DrawRect(
            target,
            new Rect2(bar.Position.X, slider.ThumbStart(model.RowValue), bar.Size.X, slider.ThumbLength),
            dragging ? Text : Tick);
    }

    /// <summary>The scrollbar along time under the rows, only while zoomed in past the whole clip.</summary>
    private void DrawTimeBar(AnimatorTimelineModel model, bool dragging)
    {
        if (!model.HasTimeBar)
            return;

        TimelineTimeBar timeBar = model.TimeBar;
        double duration = model.Clip.Duration * 1000.0;
        Rect2 bar = timeBar.Bar;
        ScrollSlider slider = timeBar.Slider(model.Axis, duration);
        canvas.DrawRect(target, bar, RowEven);
        canvas.DrawRect(
            target,
            new Rect2(slider.ThumbStart(TimelineTimeBar.Value(model.Axis, duration)), bar.Position.Y, slider.ThumbLength, bar.Size.Y),
            dragging ? Text : Tick);
    }

    private void DrawRuler(AnimatorTimelineModel model)
    {
        Rect2 ruler = model.Ruler;
        canvas.DrawRect(target, model.RulerBand, RulerBackground);
        if (model.Clip.Loop)
        {
            float startX = model.XForSeconds(model.Clip.LoopStart);
            float endX = model.XForSeconds(model.Clip.LoopEnd);
            canvas.DrawRect(
                target,
                new Rect2(startX, ruler.Position.Y, Math.Max(1f, endX - startX), ruler.Size.Y),
                LoopBand);
        }

        var painter = new TimelinePainter(canvas, target);
        List<TimelineTick> ticks = model.Ticks();
        painter.RulerTicks(model.Axis, ruler, ticks, RulerLook);
        painter.Labels(model.Axis, ruler, ticks, Dim);
    }

    private static readonly TimelineRulerLook RulerLook = TimelineRulerLook.TwoLevel(new(12f, Tick), new(5f, Grid));

    private void DrawRows(
        AnimatorTimelineModel model,
        AnimatorSelection selection,
        RigTrack? selected,
        RigPackage package)
    {
        if (model.Tracks.Count == 0)
        {
            canvas.DrawTextStyledLeft(
                target,
                new Rect2(model.Rows.Position.X + 8, model.Rows.Position.Y + 6, model.Rows.Size.X - 16, 18),
                "No keys yet. Pose a bone, then press KEY SELECTED (or turn on AUTO KEY).",
                Dim,
                12);
            return;
        }

        for (int screenRow = 0; screenRow < model.VisibleRows; screenRow++)
        {
            int index = model.FirstRow + screenRow;
            if (index >= model.Tracks.Count)
                break;

            AnimatorTrackRow track = model.Tracks[index];
            Rect2 row = model.RowRect(screenRow);
            // The full view is all one target: only its own row is lit, so the stripes still part the rest.
            bool sub = model.Full && (track.IsChannel || track.IsOverride);
            bool primary = !sub && selected is { } chosen && chosen == track.Track;
            bool anySelected = !sub && IsSelectedTrack(selection, package, track.Track);
            Color background = anySelected
                ? primary
                    ? track.IsChannel ? RowSelectedChannel : RowSelected
                    : RowSelectedExtra
                : screenRow % 2 == 0 ? RowEven : RowOdd;
            canvas.DrawRect(target, row, background);

            string label;
            float labelX = model.Labels.Position.X + 6 + model.Depth(track) * 12;
            bool unkeyed = false;
            if (track.Property is { } property)
            {
                label = AnimatorTrackRow.PropertyLabel(property);
                unkeyed = model.Full && AnimatorTrackRows.KeyTimes(model.Clip, track).Count == 0;
            }
            else if (model.Full && track.IsOverride)
            {
                label = "pins / ellipses";
            }
            else
            {
                label = track.IsOverride ? $"{track.Id}  (override)" : track.Target switch
                {
                    RigChannelTarget.Attachment => $"{track.Id}  (part)",
                    _ => track.Id,
                };
                if (!model.Full && !track.IsOverride && AnimatorTrackRows.HasChannels(model.Clip, track.Track))
                    label = (model.IsExpanded(track.Track) ? "▾ " : "▸ ") + label;
            }

            Color ink = unkeyed ? Unkeyed
                : primary ? Colors.White : anySelected ? Text : track.IsChannel || track.IsOverride ? Dim : Text;
            canvas.DrawTextStyledLeft(
                target,
                new Rect2(labelX, row.Position.Y + 2, AnimatorTimelineModel.LabelWidth - 8 - (labelX - model.Labels.Position.X - 6), row.Size.Y - 4),
                label,
                ink,
                11);
        }

        // Over the stripes, under the keys.
        DrawGridLines(model);
        AnimatorOverrideBars.Draw(canvas, target, model, _selectedOverride);
        for (int screenRow = 0; screenRow < model.LastRow - model.FirstRow; screenRow++)
            DrawKeys(model, selection, model.Tracks[model.FirstRow + screenRow], model.RowRect(screenRow));
    }

    /// <summary>A line down through the rows at each labelled ruler tick.</summary>
    private void DrawGridLines(AnimatorTimelineModel model) =>
        new TimelinePainter(canvas, target).GridLines(model.Axis, model.Rows.Position.Y, model.RowsBottom, model.Ticks(), Grid);

    private void DrawKeys(
        AnimatorTimelineModel model,
        AnimatorSelection selection,
        AnimatorTrackRow track,
        Rect2 row)
    {
        if (track.IsOverride)
            return;

        DrawDrivenSpans(model, track, row);
        DrawEaseBands(model, track, row);
        var painter = new TimelinePainter(canvas, target);
        foreach (float time in AnimatorTrackRows.KeyTimes(model.Clip, track))
        {
            EasePreset? ease = track.Property is { } property
                ? RigEasing.Read(model.Clip, track.Target, track.Id, property, time)
                : RigEasing.Read(model.Clip, track.Target, track.Id, time);
            painter.Key(model.Axis, row, time * 1000.0, KeyColor, ease ?? EasePreset.Linear, selection.IsKeySelected(track, time));
        }
    }

    /// <summary>A band over the time a target owns this row's keys, so edits that will not stick can be seen coming.</summary>
    private void DrawDrivenSpans(AnimatorTimelineModel model, AnimatorTrackRow track, Rect2 row)
    {
        if (track.Target != RigChannelTarget.Bone)
            return;

        RigPackage package = _package!;
        int bone = RigRig.FindBone(package, track.Id);
        foreach (RigLimbTarget limb in model.Clip.Targets)
        {
            if (!RigTargets.IsValid(package, limb) ||
                !RigTargets.Channels(package, limb).Exists(channel => channel.Bone == bone &&
                    (track.Property is null || track.Property == channel.Property)))
            {
                continue;
            }

            new TimelinePainter(canvas, target).Bar(model.Axis, row, limb.From * 1000.0, limb.To * 1000.0, DrivenBand);
        }
    }

    /// <summary>The speed band of each span between the row's keys, under the markers.</summary>
    private void DrawEaseBands(AnimatorTimelineModel model, AnimatorTrackRow track, Rect2 row)
    {
        var keys = AnimatorTrackRows.KeyTimes(model.Clip, track);
        var painter = new TimelinePainter(canvas, target);
        for (int i = 0; i + 1 < keys.Count; i++)
        {
            RigEaseSegment segment = RigEasing.ReadSegment(model.Clip, track.Target, track.Id, track.Property, keys[i]);
            painter.Span(model.Axis, row, keys[i] * 1000.0, keys[i + 1] * 1000.0, segment.Preset, EaseColor(segment), EaseReverse);
        }
    }

    /// <summary>Each easing's colour; named curves take the colour of their direction.</summary>
    private static Color EaseColor(RigEaseSegment segment) => segment.Preset switch
    {
        null => EaseLineQuiet,
        EasePreset.Step => Palette.Warning,
        EasePreset.Linear => Palette.Info,
        EasePreset.Ease => Palette.Lime,
        { } preset => EasePresets.Shape(preset)?.Direction switch
        {
            CurveDirection.In => Palette.Pink,
            CurveDirection.Out => Palette.Violet,
            CurveDirection.InOut => Palette.Lime,
            _ => EaseLineQuiet,
        },
    };

    /// <summary>Whether a track belongs to any selected bone, attachment or effect, not just the primary.</summary>
    private static bool IsSelectedTrack(
        AnimatorSelection selection,
        RigPackage package,
        RigTrack track)
    {
        if (track.Target == RigChannelTarget.Bone)
            return selection.IsBoneSelected(RigRig.FindBone(package, track.Id));
        int attachment = package.Attachments.FindIndex(candidate => candidate.Id == track.Id);
        return selection.Attachment >= 0 && selection.Attachment == attachment;
    }

    /// <summary>
    /// Marks the loop range through the rows, with a grab tab at each end. The tabs
    /// sit in the ruler's lower half so they never cover the playhead's tab on top.
    /// </summary>
    private void DrawLoop(AnimatorTimelineModel model)
    {
        if (!model.Clip.Loop)
            return;

        float startX = model.XForSeconds(model.Clip.LoopStart);
        float endX = model.XForSeconds(model.Clip.LoopEnd);
        float top = model.Ruler.Position.Y;
        float bottom = model.RowsBottom;
        canvas.DrawLine(target, new Vector2(startX, top), new Vector2(startX, bottom), LoopLine, 1);
        canvas.DrawLine(target, new Vector2(endX, top), new Vector2(endX, bottom), LoopLine, 1);
        float tabTop = model.Ruler.End.Y - AnimatorTimelineModel.LoopTabHeight;
        canvas.DrawRect(target, new Rect2(startX - 4, tabTop, 8, AnimatorTimelineModel.LoopTabHeight), LoopLine);
        canvas.DrawRect(target, new Rect2(endX - 4, tabTop, 8, AnimatorTimelineModel.LoopTabHeight), LoopLine);
    }

    /// <summary>
    /// Each clip event as a flag on the ruler with its name beside it, and a
    /// faint line down through the rows so it can be lined up with keys.
    /// </summary>
    private void DrawEvents(AnimatorTimelineModel model, int selected)
    {
        Rect2 ruler = model.Ruler;
        for (int i = 0; i < model.Clip.Events.Count; i++)
        {
            RigEvent item = model.Clip.Events[i];
            float x = model.XForSeconds(item.Time);
            if (x < ruler.Position.X - 1 || x > ruler.End.X + 1)
                continue;

            Color color = i == selected ? EventSelected : EventColor;
            canvas.DrawLine(target, new Vector2(x, ruler.End.Y), new Vector2(x, model.RowsBottom), EventLine, 1);
            canvas.DrawRect(target, new Rect2(x - 1, ruler.Position.Y, 2, ruler.Size.Y), color);
            canvas.DrawRect(target, new Rect2(x + 1, ruler.Position.Y, 8, 7), color);
            if (x + 4 + 60 <= ruler.End.X)
            {
                canvas.DrawTextStyledLeft(target, new Rect2(x + 4, ruler.Position.Y + 12, 90, 12),
                    item.Name, color, 10);
            }
        }
    }

    private void DrawPlayhead(AnimatorTimelineModel model, float time) =>
        new TimelinePainter(canvas, target).Playhead(model.Axis, time * 1000.0, model.Ruler.Position.Y, model.RowsBottom, Playhead);
}
