using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The live preview inside the group overlay: the playfield at the current
/// playhead with the open group's moves and colours applied, plus the selected
/// effect's easing curve. It draws on its own canvas above the overlay.
/// </summary>
public sealed class EditorGroupPreviewSystem :
    QuerySystem<EditorGroupState, LevelGroups, Playfield, Playhead, PlayfieldView>
{
    private readonly EntityStore _entities;
    private readonly IRectCanvas _canvas;
    private readonly Rid _preview;

    public EditorGroupPreviewSystem(EntityStore entities, IRectCanvas canvas, Rid preview)
    {
        _entities = entities;
        _canvas = canvas;
        _preview = preview;
    }

    protected override void OnUpdate()
    {
        _canvas.Clear(_preview);

        Query.ForEachEntity((
            ref state,
            ref levelGroups,
            ref playfield,
            ref playhead,
            ref view,
            entity) =>
        {
            if (state.Open < 0 || state.Open >= levelGroups.Groups.Length)
                return;

            Vector2 screen = view.ScreenSize;
            Vector2 dialog = EditorGroupLayout.DialogSize(screen);
            Vector2 origin = (screen - dialog) / 2;
            Rect2 area = Offset(EditorGroupLayout.PreviewArea(dialog), origin);
            DrawPreview(area, state, levelGroups, playfield, playhead);
            DrawCurve(state, levelGroups, dialog, origin);
            if (state.PaletteOpen)
                DrawPaletteFrame(dialog, origin);
        });
    }

    private void DrawPreview(
        Rect2 area,
        EditorGroupState state,
        LevelGroups levelGroups,
        Playfield playfield,
        Playhead playhead)
    {
        _canvas.DrawRect(_preview, area.Grow(2), Palette.Muted);
        _canvas.DrawRect(_preview, area, PlayfieldRenderer.PlayfieldBackground);

        float scale = Mathf.Min(
            area.Size.X / Mathf.Max(1, playfield.Width),
            area.Size.Y / Mathf.Max(1, playfield.Height));
        Vector2 fieldSize = new(playfield.Width * scale, playfield.Height * scale);
        Vector2 fieldOrigin = area.Position + (area.Size - fieldSize) / 2;
        Rect2 field = new(fieldOrigin, fieldSize);
        _canvas.DrawRect(_preview, field, PlayfieldRenderer.PlayfieldBackground);
        CanvasGrid.Draw(
            _canvas,
            _preview,
            field,
            field.Size / 10,
            field.Position + field.Size / 2);
        PlayfieldRenderer.DrawBorder(_canvas, _preview, field, PlayfieldRenderer.BorderColor, 1);

        var view = new PlayfieldView(fieldOrigin, scale, area.Size);
        float seconds = playhead.TimeMs / 1000f;
        uint solo = state.SoloGroup is >= 0 and < GroupLimits.Count
            ? 1u << state.SoloGroup
            : 0u;

        DrawBlocks(view, levelGroups.Groups, seconds, solo);
        DrawNotes(view, levelGroups.Groups, seconds, solo, playhead);
        DrawMines(view, levelGroups.Groups, seconds, solo, playhead);
        DrawStarts(view, levelGroups.Groups, seconds, solo);

        _canvas.DrawRect(
            _preview,
            new Rect2(area.Position + new Vector2(4, 4), new Vector2(92, 15)),
            new Color(0.02f, 0.05f, 0.1f, 0.72f));
        _canvas.DrawTextLeft(
            _preview,
            new Rect2(area.Position + new Vector2(6, 4), new Vector2(90, 15)),
            solo == 0 ? "LIVE" : "LIVE · SOLO",
            Palette.TextDim);
    }

    private void DrawBlocks(
        PlayfieldView view,
        GroupDefinition[] groups,
        float seconds,
        uint solo)
    {
        foreach (Entity entity in _entities.Query<Block>().ToEntityList())
        {
            Block block = entity.GetComponent<Block>();
            if (block.Size.X <= 0 || block.Size.Y <= 0)
                continue;

            MaskOf(entity, solo, out uint mask, out bool inSolo);
            Vector2 offset = mask == 0
                ? Vector2.Zero
                : GroupModulation.Offset(groups, mask, seconds);
            Color edge = inSolo
                ? Tint(groups, mask, seconds, PlayfieldRenderer.BlockEdge)
                : PlayfieldRenderer.BlockEdge;
            Color fill = inSolo
                ? Tint(groups, mask, seconds, PlayfieldRenderer.BlockFill)
                : PlayfieldRenderer.BlockFill;
            float alpha = inSolo ? 1f : 0.28f;
            edge.A *= alpha;
            fill.A *= alpha;

            var screen = new Rect2(
                view.PlayfieldToScreen(block.Position + offset),
                block.Size * view.Scale);
            _canvas.DrawRect(_preview, screen, edge);
            Rect2 inner = screen.Grow(-1);
            if (inner.Size.X > 0 && inner.Size.Y > 0)
                _canvas.DrawRect(_preview, inner, fill);
        }
    }

    private void DrawNotes(
        PlayfieldView view,
        GroupDefinition[] groups,
        float seconds,
        uint solo,
        Playhead playhead)
    {
        float radius = PlayfieldRenderer.NoteSize * view.Scale / 2f;
        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            float fade = NoteFade.Alpha(note, playhead.TimeMs);
            if (fade <= 0f)
                continue;

            MaskOf(entity, solo, out uint mask, out bool inSolo);
            Vector2 offset = mask == 0
                ? Vector2.Zero
                : GroupModulation.Offset(groups, mask, seconds);
            Color color = inSolo
                ? Tint(groups, mask, seconds, PlayfieldRenderer.ColorFor(note))
                : PlayfieldRenderer.ColorFor(note);
            float groupAlpha = color.A;
            color.A = fade * (inSolo ? 0.9f : 0.25f) * groupAlpha;
            Vector2 center = view.PlayfieldToScreen(note.Position + offset);

            if (radius < 7f)
            {
                _canvas.DrawCircle(_preview, center, Mathf.Max(1.5f, radius), color);
                continue;
            }

            PlayfieldRenderer.DrawNoteBody(_canvas, _preview, note, center, radius, color);
        }
    }

    private void DrawMines(
        PlayfieldView view,
        GroupDefinition[] groups,
        float seconds,
        uint solo,
        Playhead playhead)
    {
        float size = PlayfieldRenderer.MineSize * view.Scale;
        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
        {
            Mine mine = entity.GetComponent<Mine>();
            float fade = MineFade.Alpha(mine, playhead.TimeMs);
            if (fade <= 0f)
                continue;

            MaskOf(entity, solo, out uint mask, out bool inSolo);
            Vector2 offset = mask == 0
                ? Vector2.Zero
                : GroupModulation.Offset(groups, mask, seconds);
            Color color = inSolo
                ? Tint(groups, mask, seconds, new Color(1f, 0.22f, 0.16f))
                : new Color(1f, 0.22f, 0.16f);
            float groupAlpha = color.A;
            color.A = fade * (inSolo ? 0.9f : 0.25f) * groupAlpha;
            Vector2 center = view.PlayfieldToScreen(mine.Position + offset);
            var bounds = new Rect2(
                center - new Vector2(size / 2f, size / 2f),
                new Vector2(size, size));
            _canvas.DrawRect(_preview, bounds, new Color(0.08f, 0.02f, 0.02f, color.A * 0.8f));
            PlayfieldRenderer.DrawBorder(_canvas, _preview, bounds, color, 1);
        }
    }

    private void DrawStarts(
        PlayfieldView view,
        GroupDefinition[] groups,
        float seconds,
        uint solo)
    {
        foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
        {
            StartPoint start = entity.GetComponent<StartPoint>();
            MaskOf(entity, solo, out uint mask, out bool inSolo);
            Vector2 offset = mask == 0
                ? Vector2.Zero
                : GroupModulation.Offset(groups, mask, seconds);
            Color color = inSolo
                ? Tint(groups, mask, seconds, PlayfieldRenderer.StartPointColor)
                : PlayfieldRenderer.StartPointColor;
            float groupAlpha = color.A;
            color.A = (inSolo ? 1f : 0.25f) * groupAlpha;
            Vector2 center = view.PlayfieldToScreen(start.Position + offset);
            _canvas.DrawCircleOutline(_preview, center, 5, color, 1);
            _canvas.DrawTextStyled(
                _preview,
                new Rect2(center - new Vector2(6, 6), new Vector2(12, 12)),
                "S",
                color,
                8);
        }
    }

    /// <summary>The item's group mask after applying solo, and whether it is in the solo group.</summary>
    private static void MaskOf(Entity entity, uint solo, out uint mask, out bool inSolo)
    {
        uint all = entity.TryGetComponent(out ItemGroups groups) ? groups.Mask : 0u;
        inSolo = solo == 0 || (all & solo) != 0;
        mask = solo == 0 ? all : all & solo;
    }

    private static Color Tint(
        GroupDefinition[] groups,
        uint mask,
        float seconds,
        Color basis) =>
        GroupModulation.TryColor(groups, mask, seconds, out Color mix, out float coverage)
            ? new GroupColor(mix, coverage).Apply(basis)
            : basis;

    /// <summary>Draws the selected effect's easing curve in the selection bar.</summary>
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
        _canvas.DrawRect(_preview, area.Grow(1), Palette.Muted);
        _canvas.DrawRect(_preview, area, Palette.SurfaceInset);

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
        _canvas.DrawPolyline(_preview, points, color, 1.5f);
    }

    /// <summary>A thin frame around the floating palette; its swatches are ECS buttons.</summary>
    private void DrawPaletteFrame(Vector2 dialog, Vector2 origin)
    {
        Rect2 area = Offset(EditorGroupLayout.PalettePanel(dialog), origin);
        Color border = Palette.BorderSubtle;
        _canvas.DrawRect(_preview, new Rect2(area.Position, new Vector2(area.Size.X, 2)), border);
        _canvas.DrawRect(_preview, new Rect2(new Vector2(area.Position.X, area.End.Y - 2), new Vector2(area.Size.X, 2)), border);
        _canvas.DrawRect(_preview, new Rect2(area.Position, new Vector2(2, area.Size.Y)), border);
        _canvas.DrawRect(_preview, new Rect2(new Vector2(area.End.X - 2, area.Position.Y), new Vector2(2, area.Size.Y)), border);
    }

    private static Rect2 Offset(Rect2 rect, Vector2 by) => new(rect.Position + by, rect.Size);
}
