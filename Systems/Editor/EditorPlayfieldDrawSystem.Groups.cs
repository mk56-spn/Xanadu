using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Systems.Playfields;

namespace Writer.Systems.Editor;

/// <summary>The editor's group preview: translucent ghosts where groups will move and tint items.</summary>
public sealed partial class EditorPlayfieldDrawSystem
{
    /// <summary>Where the groups would put each member at the playhead, drawn over the editable item.</summary>
    private void DrawGroupPreview(PlayfieldView view, Playhead playhead, LevelGroups groups)
    {
        if (groups.Groups is null)
            return;

        float seconds = playhead.TimeMs / 1000f;
        DrawPreviewNotes(view, playhead, groups, seconds);
        DrawPreviewMines(view, playhead, groups, seconds);
        DrawPreviewBlocks(view, groups, seconds);
        DrawPreviewStartPoints(view, groups, seconds);
    }

    private void DrawPreviewNotes(PlayfieldView view, Playhead playhead, LevelGroups groups, float seconds)
    {
        float radius = PlayfieldRenderer.NoteSize * view.Scale / 2f;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (!entity.TryGetComponent(out ItemGroups member) || member.Mask == 0)
                continue;

            Note note = entity.GetComponent<Note>();
            float fade = NoteFade.Alpha(note, playhead.TimeMs);
            if (fade <= 0f)
                continue;

            Vector2 offset = GroupModulation.Offset(groups.Groups, member.Mask, seconds);
            bool tinted = GroupModulation.TryColor(
                groups.Groups, member.Mask, seconds, out Color mix, out float coverage);
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

    private void DrawPreviewMines(PlayfieldView view, Playhead playhead, LevelGroups groups, float seconds)
    {
        float size = PlayfieldRenderer.MineSize * view.Scale;

        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
        {
            if (!entity.TryGetComponent(out ItemGroups member) || member.Mask == 0)
                continue;

            Mine mine = entity.GetComponent<Mine>();
            float fade = MineFade.Alpha(mine, playhead.TimeMs);
            if (fade <= 0f)
                continue;

            Vector2 offset = GroupModulation.Offset(groups.Groups, member.Mask, seconds);
            bool tinted = GroupModulation.TryColor(
                groups.Groups, member.Mask, seconds, out Color mix, out float coverage);
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

    private void DrawPreviewBlocks(PlayfieldView view, LevelGroups groups, float seconds)
    {
        foreach (Entity entity in _entities.Query<Block>().ToEntityList())
        {
            if (!entity.TryGetComponent(out ItemGroups member) || member.Mask == 0)
                continue;

            Block block = entity.GetComponent<Block>();
            if (block.Size.X <= 0 || block.Size.Y <= 0)
                continue;

            Vector2 offset = GroupModulation.Offset(groups.Groups, member.Mask, seconds);
            bool tinted = GroupModulation.TryColor(
                groups.Groups, member.Mask, seconds, out Color mix, out float coverage);
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

    private void DrawPreviewStartPoints(PlayfieldView view, LevelGroups groups, float seconds)
    {
        foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
        {
            if (!entity.TryGetComponent(out ItemGroups member) || member.Mask == 0)
                continue;

            StartPoint start = entity.GetComponent<StartPoint>();
            Vector2 offset = GroupModulation.Offset(groups.Groups, member.Mask, seconds);
            bool tinted = GroupModulation.TryColor(
                groups.Groups, member.Mask, seconds, out Color mix, out float coverage);
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
