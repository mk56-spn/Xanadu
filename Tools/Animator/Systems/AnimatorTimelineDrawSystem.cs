using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Animation;
using Writer.Ecs;
using Writer.Levels;
using Writer.Systems.Canvas;

namespace Writer.Tools.Animator.Systems;

/// <summary>Draws the keyframe timeline: ruler, track rows, key diamonds and the playhead.</summary>
public sealed class AnimatorTimelineDrawSystem(IRectCanvas canvas, Rid target)
    : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private static readonly Color RulerBackground = new("#0b1220");
    private static readonly Color RowEven = new("#0f172a");
    private static readonly Color RowOdd = new("#131d33");
    private static readonly Color RowSelected = new("#164e63");
    private static readonly Color Grid = new("#1e293b");
    private static readonly Color Tick = new("#64748b");
    private static readonly Color Dim = new("#94a3b8");
    private static readonly Color Text = new("#e2e8f0");
    private static readonly Color KeyColor = new("#38bdf8");
    private static readonly Color Playhead = new("#facc15");

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

        DrawHeader(model, document.Runtime);
        DrawRuler(model);
        DrawRows(model, selection, selected);
        DrawPlayhead(model, document.Runtime.CurrentTime);
    }

    private void DrawHeader(AnimatorTimelineModel model, AnimatorRuntime runtime)
    {
        string rows = model.Tracks.Count > model.VisibleRows
            ? $"   rows {model.FirstRow + 1}-{model.LastRow} of {model.Tracks.Count}"
            : "";
        canvas.DrawTextStyledLeft(
            target,
            new Rect2(
                model.Panel.Position.X + AnimatorTimelineModel.Padding,
                model.Panel.Position.Y + AnimatorTimelineModel.Padding,
                model.Panel.Size.X - AnimatorTimelineModel.Padding * 2,
                AnimatorTimelineModel.HeaderHeight),
            $"{model.Clip.Id}   {runtime.CurrentTime:0.00}s / {model.Clip.Duration:0.00}s" +
            $"   zoom x{model.Zoom:0.#}   snap {model.SnapStep:0.###}s (Shift = free){rows}",
            Dim,
            12);
    }

    private void DrawRuler(AnimatorTimelineModel model)
    {
        Rect2 ruler = model.Ruler;
        canvas.DrawRect(target, ruler, RulerBackground);

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

            canvas.DrawLine(
                target,
                new Vector2(x, model.Rows.Position.Y),
                new Vector2(x, model.Rows.Position.Y + model.VisibleRows * AnimatorTimelineModel.RowHeight),
                Grid,
                1);
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
        XafTrack? selected)
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

            XafTrack track = model.Tracks[index];
            Rect2 row = model.RowRect(screenRow);
            bool isSelected = selected is { } chosen && chosen == track;
            canvas.DrawRect(target, row, isSelected ? RowSelected : screenRow % 2 == 0 ? RowEven : RowOdd);
            canvas.DrawTextStyledLeft(
                target,
                new Rect2(model.Labels.Position.X + 6, row.Position.Y + 2, AnimatorTimelineModel.LabelWidth - 8, row.Size.Y - 4),
                track.Target == XafChannelTarget.Attachment ? $"{track.Id}  (svg)" : track.Id,
                isSelected ? Colors.White : Text,
                12);
            DrawKeys(model, selection, track, row);
        }
    }

    private void DrawKeys(
        AnimatorTimelineModel model,
        AnimatorSelection selection,
        XafTrack track,
        Rect2 row)
    {
        foreach (float time in XafTracks.KeyTimes(model.Clip, track))
        {
            double ms = time * 1000.0;
            if (ms < model.Window.Start || ms > model.Window.End)
                continue;

            bool chosen = selection.HasKey &&
                          selection.KeyTarget == track.Target &&
                          selection.KeyId == track.Id &&
                          Math.Abs(selection.KeyTime - time) <= XafTracks.TimeTolerance;
            XafEasePreset? ease = XafEasing.Read(model.Clip, track.Target, track.Id, time);
            DrawKey(
                new Vector2(model.XForSeconds(time), row.Position.Y + row.Size.Y / 2),
                chosen ? AnimatorTimelineModel.KeyRadius + 1 : AnimatorTimelineModel.KeyRadius - 1,
                chosen ? Colors.White : KeyColor,
                ease ?? XafEasePreset.Linear);
        }
    }

    private void DrawPlayhead(AnimatorTimelineModel model, float time)
    {
        float x = model.XForSeconds(time);
        float top = model.Ruler.Position.Y;
        float bottom = model.Rows.Position.Y + model.VisibleRows * AnimatorTimelineModel.RowHeight;
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
