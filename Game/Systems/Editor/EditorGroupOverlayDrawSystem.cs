using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Rendering;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Timeline;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The group overlay's own drawing: the effects on the timeline as ease bands
/// and pins (the animator's style), the selected effect's easing curve and the
/// palette's frame, on a canvas above the dialog. What the groups do to the
/// level shows live in the editor view itself (Docs/render-architecture.md D18b).
/// </summary>
public sealed class EditorGroupOverlayDrawSystem :
    QuerySystem<EditorGroupState, LevelGroups, PlayfieldView>
{
    private readonly IRectCanvas _canvas;
    private readonly Handle _overlay;

    public EditorGroupOverlayDrawSystem(IRectCanvas canvas, Handle overlay)
    {
        _canvas = canvas;
        _overlay = overlay;
    }

    protected override void OnUpdate()
    {
        _canvas.Clear(_overlay);

        Query.ForEachEntity((ref EditorGroupState state, ref LevelGroups levelGroups, ref PlayfieldView view, Entity editor) =>
        {
            if (state.Open < 0 || state.Open >= levelGroups.Groups.Length)
                return;

            Vector2 screen = view.ScreenSize;
            Vector2 dialog = EditorGroupLayout.DialogSize(screen);
            Vector2 origin = EditorGroupLayout.Origin(screen);
            bool timed = editor.TryGetComponent(out Playhead playhead);
            TempoMap tempo = timed ? playhead.Tempo : TempoMap.Default;
            DrawRuler(state, dialog, origin, tempo);
            DrawCurve(state, levelGroups, dialog, origin);
            if (state.PaletteOpen)
                DrawPaletteFrame(dialog, origin);
            else
            {
                DrawEffects(state, levelGroups.Groups[state.Open], dialog, origin, tempo);
                DrawKeyLane(editor, state, levelGroups.Groups[state.Open], dialog, origin);
            }

            if (timed)
                DrawPlayhead(state, dialog, origin, playhead.TimeMs);
        });
    }

    /// <summary>Lines through the rows and the key lane on the song's beat grid (the note timeline's), with second labels above.</summary>
    private void DrawRuler(EditorGroupState state, Vector2 dialog, Vector2 origin, TempoMap tempo)
    {
        Rect2 timeline = Offset(EditorGroupLayout.Timeline(dialog), origin);
        Rect2 ruler = Offset(EditorGroupLayout.Ruler(dialog), origin);
        TimelineAxis axis = EditorGroupLayout.Axis(timeline, state.ViewStart, state.ViewSpan);
        Painter.GridLines(
            axis,
            timeline.Position.Y,
            timeline.Position.Y + EditorGroupLayout.LinesHeight,
            TempoTimelineTicks.For(axis, tempo),
            BeatLine,
            TickLine);
        Painter.Labels(
            EditorGroupLayout.Axis(ruler, state.ViewStart, state.ViewSpan),
            ruler,
            TimelineTicks.Seconds(axis, LabelPixels, LabelPixels, "s"),
            Palette.TextDim);
    }

    private void DrawPlayhead(EditorGroupState state, Vector2 dialog, Vector2 origin, long timeMs)
    {
        Rect2 timeline = Offset(EditorGroupLayout.Timeline(dialog), origin);
        TimelineAxis axis = EditorGroupLayout.Axis(timeline, state.ViewStart, state.ViewSpan);
        Painter.Playhead(axis, timeMs, timeline.Position.Y, timeline.Position.Y + EditorGroupLayout.LinesHeight, Colors.White);
    }

    private static readonly Color BeatLine = new(1f, 1f, 1f, 0.35f);
    private static readonly Color TickLine = new(1f, 1f, 1f, 0.12f);

    /// <summary>The least room between second labels.</summary>
    private const double LabelPixels = 70;

    private TimelinePainter Painter => new(_canvas, _overlay);

    /// <summary>
    /// Each effect on its row the way the animator draws a span between keys:
    /// a band whose thickness follows the effect's easing, between a pin at its
    /// start and one at its end, shaped like the animator's keys (square for a
    /// step, diamond when linear, circle when eased).
    /// The pins are the grips that retime it; the band moves it whole.
    /// </summary>
    private void DrawEffects(EditorGroupState state, GroupDefinition group, Vector2 dialog, Vector2 origin, TempoMap tempo)
    {
        Rect2 timeline = Offset(EditorGroupLayout.Timeline(dialog), origin);
        for (int row = 0; row < group.Effects.Count && row < EditorGroupLayout.MaxRows; row++)
        {
            GroupEffect effect = group.Effects[row];
            bool selected = state.SelectedGroup == state.Open && state.SelectedEffect == row;
            Color color = effect.Kind == GroupEffectKind.Move
                ? Palette.Info
                : new Color(effect.Tint.R, effect.Tint.G, effect.Tint.B);
            if (effect.Muted)
                color = color.Darkened(0.55f);

            Rect2 rowRect = EditorGroupLayout.RowRect(row, timeline);
            TimelineAxis axis = EditorGroupLayout.Axis(timeline, state.ViewStart, state.ViewSpan);
            Func<float, float> shape = Shape(effect);
            Color band = new(color, selected ? 0.9f : 0.6f);
            if (effect.Cycling && effect.Cycle == GroupCycle.Return)
                Painter.Span(axis, rowRect, effect.Start * 1000.0, effect.End * 1000.0, shape, band);
            else
                Painter.Span(axis, rowRect, effect.Start * 1000.0, effect.End * 1000.0, effect.Ease, band);

            if (effect.Cycling)
                DrawRepeats(state, effect, timeline, rowRect, shape, new Color(color, selected ? 0.4f : 0.25f), tempo);

            Painter.Key(axis, rowRect, effect.Start * 1000.0, color, effect.Ease, selected);
            Painter.Key(axis, rowRect, effect.End * 1000.0, color, effect.Ease, selected);
        }
    }

    /// <summary>
    /// The selected shader setting's keys on the lane under the effects: a pin
    /// per key, and between neighbours a band whose thickness follows the
    /// earlier key's easing, the way the effects and the animator draw spans.
    /// </summary>
    private void DrawKeyLane(Entity editor, EditorGroupState state, GroupDefinition group, Vector2 dialog, Vector2 origin)
    {
        Rect2 area = Offset(EditorGroupLayout.KeyLane(dialog), origin);
        float middle = area.Position.Y + area.Size.Y / 2f;
        _canvas.DrawRect(_overlay, new Rect2(area.Position.X, middle, area.Size.X, 1f), LaneLine);
        if (!EditorGroupSystem.TrySelectedSetting(editor, state, group, out InstanceUniform uniform, out List<ShaderKey> keys))
            return;

        List<ShaderKey> lane = EditorShaderKeyEdits.Lane(keys, uniform.Name);
        TimelineAxis axis = EditorGroupLayout.Axis(area, state.ViewStart, state.ViewSpan);
        Color color = Palette.Accent;
        for (int i = 0; i + 1 < lane.Count; i++)
            Painter.Span(axis, area, lane[i].Time * 1000.0, lane[i + 1].Time * 1000.0, lane[i].Ease, new Color(color, 0.45f));

        foreach (ShaderKey key in lane)
        {
            bool selected = state.SelectedKey >= 0f &&
                            System.Math.Abs(key.Time - state.SelectedKey) < EditorShaderKeyEdits.SameMoment;
            Painter.Key(axis, area, key.Time * 1000.0, color, key.Ease, selected);
        }
    }

    private static readonly Color LaneLine = new(1f, 1f, 1f, 0.18f);

    /// <summary>The band's thickness over one cycle: the easing, or out and back for RETURN.</summary>
    private static Func<float, float> Shape(GroupEffect effect)
    {
        EasePreset ease = effect.Ease;
        if (effect.Cycling && effect.Cycle == GroupCycle.Return)
            return t => EasePresets.Progress(ease, t < 0.5f ? t * 2f : 2f - t * 2f);
        return t => EasePresets.Progress(ease, t);
    }

    /// <summary>
    /// Each later cycle as a fainter copy of the band, with a tick where it
    /// starts, so the repeat reads on the timeline. Only the cycles in view are
    /// drawn.
    /// </summary>
    private void DrawRepeats(
        EditorGroupState state,
        GroupEffect effect,
        Rect2 timeline,
        Rect2 rowRect,
        Func<float, float> shape,
        Color ghost,
        TempoMap tempo)
    {
        double first = tempo.BeatAt(effect.Start * 1000.0);
        double viewFrom = tempo.BeatAt(state.ViewStart * 1000.0);
        int from = Math.Max(1, (int)Math.Floor((viewFrom - first) / effect.EveryBeats) - 1);
        for (int k = from; k < effect.Cycles && k < from + 512; k++)
        {
            (float start, float end) = effect.CycleSpan(k, tempo);
            if (start > state.ViewStart + state.ViewSpan)
                break;
            if (end < state.ViewStart)
                continue;

            TimelineAxis axis = EditorGroupLayout.Axis(timeline, state.ViewStart, state.ViewSpan);
            Painter.Span(axis, rowRect, start * 1000.0, end * 1000.0, shape, ghost);
            float fromX = axis.X(start * 1000.0);
            if (fromX >= timeline.Position.X && fromX <= timeline.End.X)
                _canvas.DrawRect(_overlay, new Rect2(fromX, rowRect.Position.Y + 6f, 1f, rowRect.Size.Y - 12f), ghost);
        }
    }

    private void DrawCurve(
        EditorGroupState state,
        LevelGroups levelGroups,
        Vector2 dialog,
        Vector2 origin)
    {
        if (state.SelectedGroup != state.Open ||
            state.SelectedGroup < 0 ||
            state.SelectedGroup >= levelGroups.Groups.Length)
        {
            return;
        }

        GroupDefinition group = levelGroups.Groups[state.SelectedGroup];
        if (state.SelectedEffect < 0 || state.SelectedEffect >= group.Effects.Count)
            return;

        GroupEffect effect = group.Effects[state.SelectedEffect];
        Rect2 area = Offset(EditorGroupLayout.CurveArea(dialog), origin);
        _canvas.DrawRect(_overlay, area.Grow(1), Palette.Muted);
        _canvas.DrawRect(_overlay, area, Palette.SurfaceInset);

        var points = new Vector2[21];
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (float)(points.Length - 1);
            float eased = EasePresets.Progress(effect.Ease, t);
            points[i] = new Vector2(
                area.Position.X + 3 + t * (area.Size.X - 6),
                area.End.Y - 3 - eased * (area.Size.Y - 6));
        }

        Color color = effect.Kind == GroupEffectKind.Move
            ? Palette.Info
            : new Color(effect.Tint.R, effect.Tint.G, effect.Tint.B);
        if (effect.Muted)
            color = color.Darkened(0.55f);
        _canvas.DrawPolyline(_overlay, points, color, 1.5f);
    }

    /// <summary>A thin frame around the floating palette; its swatches are ECS buttons.</summary>
    private void DrawPaletteFrame(Vector2 dialog, Vector2 origin)
    {
        Rect2 area = Offset(EditorGroupLayout.PalettePanel(dialog), origin);
        Color border = Palette.BorderSubtle;
        _canvas.DrawRect(_overlay, new Rect2(area.Position, new Vector2(area.Size.X, 2)), border);
        _canvas.DrawRect(_overlay, new Rect2(new Vector2(area.Position.X, area.End.Y - 2), new Vector2(area.Size.X, 2)), border);
        _canvas.DrawRect(_overlay, new Rect2(area.Position, new Vector2(2, area.Size.Y)), border);
        _canvas.DrawRect(_overlay, new Rect2(new Vector2(area.End.X - 2, area.Position.Y), new Vector2(2, area.Size.Y)), border);
    }

    private static Rect2 Offset(Rect2 rect, Vector2 by) => new(rect.Position + by, rect.Size);
}
