using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>The editor's group preview: translucent ghosts where groups will move and tint items.</summary>
public sealed partial class EditorPlayfieldDrawSystem
{
    /// <summary>Where the groups would put each member at the playhead, drawn over the editable item.</summary>
    private void DrawGroupPreview(
        PlayfieldView view,
        Playhead playhead,
        LevelGroups groups,
        EditorGroupState state)
    {
        if (groups.Groups is null)
            return;

        float seconds = playhead.TimeMs / 1000f;
        DrawPreviewNotes(view, playhead, groups, state, seconds);
        DrawPreviewMines(view, playhead, groups, state, seconds);
        DrawPreviewBlocks(view, groups, state, seconds);
        DrawPreviewStartPoints(view, groups, state, seconds);
    }

    private void DrawPreviewNotes(
        PlayfieldView view,
        Playhead playhead,
        LevelGroups groups,
        EditorGroupState state,
        float seconds)
    {
        float radius = PlayfieldRenderer.NoteSize * view.Scale / 2f;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (!entity.TryGetComponent(out ItemGroups member) ||
                Effective(state, member.Mask) == 0)
            {
                continue;
            }

            uint mask = Effective(state, member.Mask);
            Note note = entity.GetComponent<Note>();
            float fade = NoteFade.Alpha(note, playhead.TimeMs);
            if (fade <= 0f)
                continue;

            Vector2 offset = GroupModulation.Offset(groups.Groups, mask, seconds);
            bool tinted = GroupModulation.TryColor(
                groups.Groups, mask, seconds, out Color mix, out float coverage);
            if (offset == Vector2.Zero && !tinted)
                continue;

            Color color = tinted
                ? new GroupColor(mix, coverage).Apply(PlayfieldRenderer.ColorFor(note))
                : PlayfieldRenderer.ColorFor(note);
            float groupAlpha = color.A;
            color.A = fade * 0.55f * groupAlpha;
            Vector2 baseCenter = view.PlayfieldToScreen(note.Position);
            Vector2 ghost = view.PlayfieldToScreen(note.Position + offset);

            DrawTether(baseCenter, ghost, fade);
            PlayfieldRenderer.DrawNoteBody(_canvas, _editor, note, ghost, radius, color);
            _canvas.DrawCircleOutline(
                _editor,
                ghost,
                radius + 3f,
                new Color(color.R, color.G, color.B, fade * 0.28f),
                1);
        }
    }

    private void DrawPreviewMines(
        PlayfieldView view,
        Playhead playhead,
        LevelGroups groups,
        EditorGroupState state,
        float seconds)
    {
        float size = PlayfieldRenderer.MineSize * view.Scale;

        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
        {
            if (!entity.TryGetComponent(out ItemGroups member) ||
                Effective(state, member.Mask) == 0)
            {
                continue;
            }

            uint mask = Effective(state, member.Mask);
            Mine mine = entity.GetComponent<Mine>();
            float fade = MineFade.Alpha(mine, playhead.TimeMs);
            if (fade <= 0f)
                continue;

            Vector2 offset = GroupModulation.Offset(groups.Groups, mask, seconds);
            bool tinted = GroupModulation.TryColor(
                groups.Groups, mask, seconds, out Color mix, out float coverage);
            if (offset == Vector2.Zero && !tinted)
                continue;

            Color color = tinted
                ? new GroupColor(mix, coverage).Apply(new Color(1f, 0.22f, 0.16f))
                : new Color(1f, 0.22f, 0.16f);
            float groupAlpha = color.A;
            color.A = fade * 0.65f * groupAlpha;
            Vector2 center = view.PlayfieldToScreen(mine.Position);
            Vector2 ghost = view.PlayfieldToScreen(mine.Position + offset);
            var bounds = new Rect2(
                ghost - new Vector2(size / 2f, size / 2f),
                new Vector2(size, size));

            DrawTether(center, ghost, fade);
            _canvas.DrawRect(
                _editor,
                bounds,
                new Color(0.08f, 0.02f, 0.02f, fade * 0.18f));
            PlayfieldRenderer.DrawBorder(_canvas, _editor, bounds, color, 2);
        }
    }

    private void DrawPreviewBlocks(
        PlayfieldView view,
        LevelGroups groups,
        EditorGroupState state,
        float seconds)
    {
        foreach (Entity entity in _entities.Query<Block>().ToEntityList())
        {
            if (!entity.TryGetComponent(out ItemGroups member) ||
                Effective(state, member.Mask) == 0)
            {
                continue;
            }

            uint mask = Effective(state, member.Mask);
            Block block = entity.GetComponent<Block>();
            if (block.Size.X <= 0 || block.Size.Y <= 0)
                continue;

            Vector2 offset = GroupModulation.Offset(groups.Groups, mask, seconds);
            bool tinted = GroupModulation.TryColor(
                groups.Groups, mask, seconds, out Color mix, out float coverage);
            if (offset == Vector2.Zero && !tinted)
                continue;

            Color edge = tinted
                ? new GroupColor(mix, coverage).Apply(PlayfieldRenderer.BlockEdge)
                : PlayfieldRenderer.BlockEdge;
            float groupAlpha = edge.A;
            edge.A = 0.7f * groupAlpha;
            var screen = new Rect2(
                view.PlayfieldToScreen(block.Position + offset),
                block.Size * view.Scale);

            PlayfieldRenderer.DrawBorder(_canvas, _editor, screen, edge, 2);
            _canvas.DrawRect(
                _editor,
                screen.Grow(-2),
                new Color(PlayfieldRenderer.BlockFill.R, PlayfieldRenderer.BlockFill.G, PlayfieldRenderer.BlockFill.B, 0.16f));
        }
    }

    private void DrawPreviewStartPoints(
        PlayfieldView view,
        LevelGroups groups,
        EditorGroupState state,
        float seconds)
    {
        foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
        {
            if (!entity.TryGetComponent(out ItemGroups member) ||
                Effective(state, member.Mask) == 0)
            {
                continue;
            }

            uint mask = Effective(state, member.Mask);
            StartPoint start = entity.GetComponent<StartPoint>();
            Vector2 offset = GroupModulation.Offset(groups.Groups, mask, seconds);
            bool tinted = GroupModulation.TryColor(
                groups.Groups, mask, seconds, out Color mix, out float coverage);
            if (offset == Vector2.Zero && !tinted)
                continue;

            Color color = tinted
                ? new GroupColor(mix, coverage).Apply(PlayfieldRenderer.StartPointColor)
                : PlayfieldRenderer.StartPointColor;
            float groupAlpha = color.A;
            color.A = 0.65f * groupAlpha;
            Vector2 center = view.PlayfieldToScreen(start.Position);
            Vector2 ghost = view.PlayfieldToScreen(start.Position + offset);

            DrawTether(center, ghost, 1f);
            _canvas.DrawCircleOutline(_editor, ghost, 13, color, 2);
            _canvas.DrawTextStyled(
                _editor,
                new Rect2(ghost - new Vector2(10, 10), new Vector2(20, 20)),
                "S",
                color,
                12);
        }
    }

    private static uint Effective(EditorGroupState state, uint mask) =>
        state.SoloGroup is >= 0 and < GroupLimits.Count
            ? mask & (1u << state.SoloGroup)
            : mask;

    /// <summary>The draggable handle for the selected move's full offset.</summary>
    private void DrawGroupTarget(
        PlayfieldView view,
        Playfield playfield,
        LevelGroups groups,
        EditorGroupState state,
        NoteSelection selection)
    {
        if (groups.Groups is null ||
            state.SelectedGroup < 0 ||
            state.SelectedGroup >= groups.Groups.Length)
        {
            return;
        }

        GroupDefinition group = groups.Groups[state.SelectedGroup];
        if (state.SelectedEffect < 0 || state.SelectedEffect >= group.Effects.Count)
            return;

        GroupEffect effect = group.Effects[state.SelectedEffect];
        if (effect.Kind != GroupEffectKind.Move)
            return;

        Entity member = EditorGroupTarget.AnchorMember(
            _entities, selection, state.SelectedGroup);
        Vector2 anchor = EditorGroupTarget.TryPosition(member, out Vector2 position)
            ? position
            : new Vector2(playfield.Width, playfield.Height) / 2f;
        Vector2 from = view.PlayfieldToScreen(anchor);
        Vector2 to = view.PlayfieldToScreen(anchor + effect.Offset);
        Color accent = Palette.Accent;

        _canvas.DrawLine(_editor, from, to, new Color(accent.R, accent.G, accent.B, 0.55f), 2);
        _canvas.DrawCircleOutline(_editor, to, EditorGroupTarget.HandleRadius, accent, 2);
        _canvas.DrawCircle(_editor, to, 5, accent);
        _canvas.DrawTextLeft(
            _editor,
            new Rect2(to + new Vector2(16, -26), new Vector2(130, 18)),
            $"({effect.Offset.X:0.#}, {effect.Offset.Y:0.#})",
            accent);

        Rect2 field = PlayfieldRenderer.FieldRect(view, playfield);
        var banner = new Rect2(
            field.Position + new Vector2(8, 8),
            new Vector2(Mathf.Min(field.Size.X - 16, 620), 24));
        _canvas.DrawRect(_editor, banner, new Color(0.02f, 0.05f, 0.1f, 0.82f));
        _canvas.DrawTextLeft(
            _editor,
            banner.Grow(-4),
            $"TARGET · G{state.SelectedGroup + 1} · MOVE {state.SelectedEffect + 1} · drag · Esc done · T timing",
            accent);
    }

    private void DrawTether(Vector2 from, Vector2 to, float alpha)
    {
        if (from.DistanceSquaredTo(to) < 4f)
            return;

        _canvas.DrawLine(
            _editor,
            from,
            to,
            new Color(0.75f, 0.87f, 1f, alpha * 0.18f),
            1);
    }
}
