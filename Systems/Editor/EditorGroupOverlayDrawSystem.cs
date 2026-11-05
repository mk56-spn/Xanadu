using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Timeline;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The group overlay's own drawing: the effects on the timeline as ease bands
/// and pins (the animator's style), the selected effect's easing curve and the
/// palette's frame, on a canvas above the dialog. What the groups do to the
/// level shows live in the editor view itself (docs/render-architecture.md D18b).
/// </summary>
public sealed class EditorGroupOverlayDrawSystem :
    QuerySystem<EditorGroupState, LevelGroups, PlayfieldView>
{
    /// <summary>The animator's key marker size, so pins look the same in both timelines.</summary>
    private const float KeyRadius = 6f;

    private readonly IRectCanvas _canvas;
    private readonly Rid _overlay;

    public EditorGroupOverlayDrawSystem(IRectCanvas canvas, Rid overlay)
    {
        _canvas = canvas;
        _overlay = overlay;
    }

    protected override void OnUpdate()
    {
        _canvas.Clear(_overlay);

        Query.ForEachEntity((ref EditorGroupState state, ref LevelGroups levelGroups, ref PlayfieldView view, Entity _) =>
        {
            if (state.Open < 0 || state.Open >= levelGroups.Groups.Length)
                return;

            Vector2 screen = view.ScreenSize;
            Vector2 dialog = EditorGroupLayout.DialogSize(screen);
            Vector2 origin = EditorGroupLayout.Origin(screen);
            DrawCurve(state, levelGroups, dialog, origin);
            if (state.PaletteOpen)
                DrawPaletteFrame(dialog, origin);
            else
                DrawEffects(state, levelGroups.Groups[state.Open], dialog, origin);
        });
    }

    /// <summary>
    /// Each effect on its row the way the animator draws a span between keys:
    /// a band whose thickness follows the effect's easing, between a pin at its
    /// start and one at its end (a diamond when linear, a circle when eased).
    /// The pins are the grips that retime it; the band moves it whole.
    /// </summary>
    private void DrawEffects(EditorGroupState state, GroupDefinition group, Vector2 dialog, Vector2 origin)
    {
        Rect2 timeline = Offset(EditorGroupLayout.Timeline(dialog), origin);
        float left = timeline.Position.X;
        float right = timeline.End.X;
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
            float fromX = EditorGroupLayout.XForTime(timeline, state.ViewStart, state.ViewSpan, effect.Start);
            float toX = EditorGroupLayout.XForTime(timeline, state.ViewStart, state.ViewSpan, effect.End);
            GroupEase ease = effect.Ease;
            Color band = new(color, selected ? 0.9f : 0.6f);
            foreach (EaseBandPiece piece in TimelineEaseBand.Pieces(
                         t => GroupEaseCurves.Apply(ease, t), fromX, toX, left, right, rowRect))
            {
                _canvas.DrawPolygon(_overlay, piece.Polygon, band);
            }

            float middle = rowRect.Position.Y + rowRect.Size.Y / 2f;
            float radius = selected ? KeyRadius + 1 : KeyRadius - 1;
            Color pin = selected ? Colors.White : color;
            foreach (float x in new[] { fromX, toX })
            {
                if (x >= left - radius && x <= right + radius)
                    DrawPin(new Vector2(x, middle), radius, pin, ease);
            }
        }
    }

    /// <summary>The animator's key marker: a diamond for linear timing, a circle for an eased one.</summary>
    private void DrawPin(Vector2 center, float radius, Color color, GroupEase ease)
    {
        if (ease == GroupEase.Linear)
        {
            _canvas.DrawPolygon(_overlay, new[]
            {
                center + new Vector2(0, -radius), center + new Vector2(radius, 0),
                center + new Vector2(0, radius), center + new Vector2(-radius, 0),
            }, color);
            return;
        }

        _canvas.DrawCircle(_overlay, center, radius, color);
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
            float eased = GroupEaseCurves.Apply(effect.Ease, t);
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
