using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>Draws the keyframe timeline: ruler, track rows, key diamonds and the playhead.</summary>
public sealed class AnimatorTimelineDrawSystem(IRectCanvas canvas, Rid target)
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
    private static readonly Color Playhead = Palette.Accent;
    private static readonly Color LoopBand = new("#2dd4bf", 0.18f);
    private static readonly Color LoopLine = new("#2dd4bf");

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
        DrawRows(model, selection, selected, document.Package);
        DrawLoop(model);
        DrawPlayhead(model, document.Runtime.CurrentTime);

        // The box being dragged to select keys, over everything else.
        if (session.TryGetComponent(out UiMarqueeBox box))
            CanvasMarquee.Draw(canvas, target, box);
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
        canvas.DrawRect(target, ruler, RulerBackground);
        if (model.Clip.Loop)
        {
            float startX = model.XForSeconds(model.Clip.LoopStart);
            float endX = model.XForSeconds(model.Clip.LoopEnd);
            canvas.DrawRect(
                target,
                new Rect2(startX, ruler.Position.Y, Math.Max(1f, endX - startX), ruler.Size.Y),
                LoopBand);
        }

        double tickMs = model.SnapStep * 1000.0;
        int first = TimingGrid.FirstTick(model.Window.Start, 0, tickMs);
        int last = TimingGrid.LastTick(model.Window.End, 0, tickMs);
        if (last - first > 4000)
            return;

        for (int tick = first; tick <= last; tick++)
        {
            double ms = TimingGrid.TickTime(tick, 0, tickMs);
            if (ms < model.Window.Start - 1 || ms > model.Window.End + 1)
                continue;

            double seconds = ms / 1000.0;
            bool major = IsMultiple(seconds, model.LabelStep);
            float x = model.Window.XForTime(ruler, ms);
            canvas.DrawLine(
                target,
                new Vector2(x, ruler.End.Y - (major ? 12f : 5f)),
                new Vector2(x, ruler.End.Y),
                major ? Tick : Grid,
                1);
            if (!major || x + 28 > ruler.End.X)
                continue;

            canvas.DrawTextStyledLeft(
                target,
                new Rect2(x + 3, ruler.Position.Y + 1, 60, 12),
                seconds.ToString("0.###"),
                Dim,
                11);
        }
    }

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
                label = track.Target == XafChannelTarget.Attachment
                    ? $"{track.Id}  (svg)"
                    : track.Id;
                bool hasChannels = AnimatorTrackRows.HasChannels(model.Clip, track.Track);
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
        for (int screenRow = 0; screenRow < model.LastRow - model.FirstRow; screenRow++)
            DrawKeys(model, selection, model.Tracks[model.FirstRow + screenRow], model.RowRect(screenRow));
    }

    /// <summary>A line down through the rows at each labelled ruler tick.</summary>
    private void DrawGridLines(AnimatorTimelineModel model)
    {
        double stepMs = model.LabelStep * 1000.0;
        if (stepMs <= 0)
            return;

        int first = TimingGrid.FirstTick(model.Window.Start, 0, stepMs);
        int last = TimingGrid.LastTick(model.Window.End, 0, stepMs);
        for (int tick = first; tick <= last && last - first <= 1000; tick++)
        {
            double ms = TimingGrid.TickTime(tick, 0, stepMs);
            if (ms < model.Window.Start - 1 || ms > model.Window.End + 1)
                continue;

            float x = model.Window.XForTime(model.Ruler, ms);
            canvas.DrawLine(
                target,
                new Vector2(x, model.Rows.Position.Y),
                new Vector2(x, model.RowsBottom),
                Grid,
                1);
        }
    }

    private void DrawKeys(
        AnimatorTimelineModel model,
        AnimatorSelection selection,
        AnimatorTrackRow track,
        Rect2 row)
    {
        foreach (float time in AnimatorTrackRows.KeyTimes(model.Clip, track))
        {
            if (!model.IsVisible(time))
                continue;

            bool chosen = selection.IsKeySelected(track, time);
            XafEasePreset? ease = track.Property is { } property
                ? XafEasing.Read(model.Clip, track.Target, track.Id, property, time)
                : XafEasing.Read(model.Clip, track.Target, track.Id, time);
            DrawKey(
                new Vector2(model.XForSeconds(time), row.Position.Y + row.Size.Y / 2),
                chosen ? AnimatorTimelineModel.KeyRadius + 1 : AnimatorTimelineModel.KeyRadius - 1,
                chosen ? Colors.White : KeyColor,
                ease ?? XafEasePreset.Linear);
        }
    }

    /// <summary>Whether a track belongs to any selected bone or attachment, not just the primary.</summary>
    private static bool IsSelectedTrack(
        AnimatorSelection selection,
        XafPackage package,
        XafTrack track)
    {
        if (track.Target == XafChannelTarget.Bone)
            return selection.IsBoneSelected(XafRig.FindBone(package, track.Id));

        int attachment = package.Attachments.FindIndex(candidate => candidate.Group == track.Id);
        return selection.Attachment >= 0 && selection.Attachment == attachment;
    }

    /// <summary>Marks the loop range through the rows, with a grab tab at each end.</summary>
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
        canvas.DrawRect(target, new Rect2(startX - 4, top - 2, 8, 12), LoopLine);
        canvas.DrawRect(target, new Rect2(endX - 4, top - 2, 8, 12), LoopLine);
    }

    private void DrawPlayhead(AnimatorTimelineModel model, float time)
    {
        float x = model.XForSeconds(time);
        float top = model.Ruler.Position.Y;
        float bottom = model.RowsBottom;
        canvas.DrawLine(target, new Vector2(x, top), new Vector2(x, bottom), Playhead, 2);
        canvas.DrawRect(target, new Rect2(x - 5, top - 4, 10, 10), Playhead);
    }

    /// <summary>The marker shows how the key eases: square holds, diamond is linear, circle is eased.</summary>
    private void DrawKey(Vector2 center, float radius, Color color, XafEasePreset ease)
    {
        switch (ease)
        {
            case XafEasePreset.Step:
                canvas.DrawRect(target, new Rect2(center - new Vector2(radius, radius), new Vector2(radius * 2, radius * 2)), color);
                break;
            case XafEasePreset.Linear:
                DrawDiamond(center, radius, color);
                break;
            default:
                canvas.DrawCircle(target, center, radius, color);
                break;
        }
    }

    /// <summary>A filled diamond built from one-pixel strips; the canvas has no polygon fill.</summary>
    private void DrawDiamond(Vector2 center, float radius, Color color)
    {
        for (float dy = -radius; dy <= radius; dy++)
        {
            float half = radius - Math.Abs(dy);
            canvas.DrawRect(target, new Rect2(center.X - half, center.Y + dy, half * 2 + 1, 1), color);
        }
    }

    private static bool IsMultiple(double value, double step)
    {
        double ratio = value / step;
        return Math.Abs(ratio - Math.Round(ratio)) < 1e-6;
    }
}
