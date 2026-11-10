using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Timeline;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>Draws the keyframe timeline: ruler, track rows, key diamonds and the playhead.</summary>
public sealed class AnimatorTimelineDrawSystem(IRectCanvas canvas, Handle target)
    : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private static readonly Color RulerBackground = Palette.SurfaceInset;
    private static readonly Color RowEven = Palette.SurfaceDeep;
    private static readonly Color RowOdd = new("#131d33");
    private static readonly Color RowSelected = new("#164e63");
    private static readonly Color RowSelectedExtra = new("#115e59");
    private static readonly Color RowSelectedChannel = Palette.SelectedDeep;
    private static readonly Color Grid = Palette.SurfaceAlt;
    private static readonly Color Tick = Palette.BorderSubtle;
    private static readonly Color Dim = Palette.TextDim;
    private static readonly Color Text = Palette.Text;
    private static readonly Color KeyColor = Palette.Info;
    private static readonly Color DrivenBand = new(Palette.Warning, 0.16f);
    private XafPackage? _package;
    private XafLimbTarget? _selectedOverride;
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
            AnimatorTimelineModel? model = AnimatorTimelineModel.Create(session);
            if (model is not null)
                Draw(session, model);
        }
    }

    private void Draw(Entity session, AnimatorTimelineModel model)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        XafTrack? selected = AnimatorTimelineModel.SelectedTrack(session, document.Package);

        WriteCaption(session, model, document.Runtime);
        DrawRuler(model);
        _package = document.Package;
        _selectedOverride = AnimatorTargetCommands.Selected(session);
        DrawRows(model, selection, selected, document.Package);
        DrawLoop(model);
        DrawPlayhead(model, document.Runtime.CurrentTime);
        DrawEvents(model, AnimatorEventCommands.Selected(session, out _));
        DrawRowBar(model, session.GetComponent<AnimatorDrag>().Kind == AnimatorDragKind.RowScroll);

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
        VerticalSlider slider = model.RowSlider;
        canvas.DrawRect(target, bar, RowEven);
        canvas.DrawRect(
            target,
            new Rect2(bar.Position.X, slider.ThumbTop(model.RowValue), bar.Size.X, slider.ThumbLength),
            dragging ? Text : Tick);
    }

    /// <summary>Writes the readout into the caption beside the transport; layout places it.</summary>
    private void WriteCaption(Entity session, AnimatorTimelineModel model, AnimatorRuntime runtime)
    {
        Entity label = session.GetComponent<AnimatorUi>().TimelineCaption;
        if (label.IsNull || !label.HasComponent<Text>())
            return;

        string caption = model.Caption(runtime.CurrentTime);
        ref Text text = ref label.GetComponent<Text>();
        if (text.Value == caption)
            return;

        text.Value = caption;
        CommandBuffer.AddTag<Dirty>(label.Id);
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
        XafTrack? selected,
        XafPackage package)
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
            bool primary = selected is { } chosen && chosen == track.Track;
            bool anySelected = IsSelectedTrack(selection, package, track.Track);
            Color background = anySelected
                ? primary
                    ? track.IsChannel ? RowSelectedChannel : RowSelected
                    : RowSelectedExtra
                : screenRow % 2 == 0 ? RowEven : RowOdd;
            canvas.DrawRect(target, row, background);

            string label;
            float labelX;
            if (track.Property is { } property)
            {
                label = AnimatorTrackRow.PropertyLabel(property);
                labelX = model.Labels.Position.X + 18;
            }
            else
            {
                label = track.IsOverride ? $"{track.Id}  (override)" : track.Target switch
                {
                    XafChannelTarget.Attachment => $"{track.Id}  (svg)",
                    XafChannelTarget.Effect => $"{track.Id}  (fx)",
                    _ => track.Id,
                };
                bool hasChannels = !track.IsOverride && AnimatorTrackRows.HasChannels(model.Clip, track.Track);
                if (hasChannels)
                {
                    label = (model.IsExpanded(track.Track) ? "▾ " : "▸ ") + label;
                    labelX = model.Labels.Position.X + 6;
                }
                else
                {
                    labelX = model.Labels.Position.X + 6;
                }
            }

            canvas.DrawTextStyledLeft(
                target,
                new Rect2(labelX, row.Position.Y + 2, AnimatorTimelineModel.LabelWidth - 8, row.Size.Y - 4),
                label,
                primary ? Colors.White : anySelected ? Text : track.IsChannel ? Dim : Text,
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
                ? XafEasing.Read(model.Clip, track.Target, track.Id, property, time)
                : XafEasing.Read(model.Clip, track.Target, track.Id, time);
            painter.Key(model.Axis, row, time * 1000.0, KeyColor, ease ?? EasePreset.Linear, selection.IsKeySelected(track, time));
        }
    }

    /// <summary>A band over the time a target owns this row's keys, so edits that will not stick can be seen coming.</summary>
    private void DrawDrivenSpans(AnimatorTimelineModel model, AnimatorTrackRow track, Rect2 row)
    {
        if (track.Target != XafChannelTarget.Bone)
            return;

        XafPackage package = _package!;
        int bone = XafRig.FindBone(package, track.Id);
        foreach (XafLimbTarget limb in model.Clip.Targets)
        {
            if (!XafTargets.IsValid(package, limb) ||
                !XafTargets.Channels(package, limb).Exists(channel => channel.Bone == bone &&
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
            XafEaseSegment segment = XafEasing.ReadSegment(model.Clip, track.Target, track.Id, track.Property, keys[i]);
            painter.Span(model.Axis, row, keys[i] * 1000.0, keys[i + 1] * 1000.0, segment.Preset, EaseColor(segment), EaseReverse);
        }
    }

    /// <summary>Each easing's colour; named curves take the colour of their direction.</summary>
    private static Color EaseColor(XafEaseSegment segment) => segment.Preset switch
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
        XafPackage package,
        XafTrack track)
    {
        if (track.Target == XafChannelTarget.Bone)
            return selection.IsBoneSelected(XafRig.FindBone(package, track.Id));
        if (track.Target == XafChannelTarget.Effect)
            return selection.Effect >= 0 && selection.Effect == package.Effects.FindIndex(effect => effect.Id == track.Id);

        int attachment = package.Attachments.FindIndex(candidate => candidate.Group == track.Id);
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
            XafEvent item = model.Clip.Events[i];
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
