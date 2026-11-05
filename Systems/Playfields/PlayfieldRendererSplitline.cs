using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

public static partial class PlayfieldRenderer
{
    /// <summary>
    /// Which Splitline rails show: in play only absorbed sides, in the editor
    /// (<paramref name="previewBoth"/>) both, so the mapper can see the authored
    /// rail layer.
    /// </summary>
    public static (bool Left, bool Right) RailsShown(EntityStore entities, bool previewBoth)
    {
        if (previewBoth)
            return (true, true);

        foreach (Entity entity in entities.Query<SplitlineState>().ToEntityList())
        {
            SplitlineState state = entity.GetComponent<SplitlineState>();
            return (state.LeftActive, state.RightActive);
        }

        return (false, false);
    }

    /// <summary>
    /// The shown rail lanes, each with its judgement line, the holds running
    /// on it and its label. The rail notes draw apart, at their own layers
    /// (<see cref="DrawRailNote"/>).
    /// </summary>
    public static void DrawRailLanes(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Playfield playfield,
        Playhead playhead,
        (bool Left, bool Right) shown)
    {
        if (shown.Left)
            DrawRailLane(canvas, rid, entities, view, playfield, playhead, ChannelSide.Left);
        if (shown.Right)
            DrawRailLane(canvas, rid, entities, view, playfield, playhead, ChannelSide.Right);
    }

    private static void DrawRailLane(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Playfield playfield,
        Playhead playhead,
        ChannelSide side)
    {
        Rect2 lane = SplitlineRail.LaneBounds(view, playfield, side);
        Color laneColor = side == ChannelSide.Left ? Palette.Cyan : Palette.Pink;
        canvas.DrawRect(rid, lane, new Color(
            laneColor.R,
            laneColor.G,
            laneColor.B,
            0.08f));
        canvas.DrawBorder(
            rid,
            lane,
            new Color(laneColor.R, laneColor.G, laneColor.B, 0.35f),
            1.5f);

        Rect2 field = FieldRect(view, playfield);
        float lineY = field.Position.Y + field.Size.Y * SplitlineRail.JudgementLine;
        canvas.DrawLine(
            rid,
            new Vector2(lane.Position.X, lineY),
            new Vector2(lane.End.X, lineY),
            new Color(laneColor.R, laneColor.G, laneColor.B, 0.65f),
            2f);

        float radius = SplitlineRail.NoteRadius * view.Scale;
        foreach (Entity entity in entities.Query<RailHold>().ToEntityList())
        {
            RailHold hold = entity.GetComponent<RailHold>();
            if (hold.Side != side)
                continue;

            Vector2 from = SplitlineRail.ScreenPosition(
                view,
                playfield,
                side,
                hold.Index,
                hold.StartMs,
                playhead.TimeMs);
            Vector2 to = SplitlineRail.ScreenPosition(
                view,
                playfield,
                side,
                hold.Index,
                hold.EndMs,
                playhead.TimeMs);
            canvas.DrawLine(
                rid,
                from,
                to,
                new Color(1f, 0.95f, 0.7f, 0.9f),
                radius * 0.7f);
        }

        canvas.DrawTextStyled(
            rid,
            new Rect2(
                new Vector2(lane.Position.X, lane.Position.Y - 18),
                new Vector2(lane.Size.X, 16)),
            side == ChannelSide.Left ? "L RAIL" : "R RAIL",
            new Color(laneColor.R, laneColor.G, laneColor.B, 0.8f),
            11);
    }

    /// <summary>One rail note on its lane, while its lane shows and it is near the playhead.</summary>
    public static void DrawRailNote(
        IRectCanvas canvas,
        Rid rid,
        Entity entity,
        PlayfieldView view,
        Playfield playfield,
        Playhead playhead,
        (bool Left, bool Right) shown)
    {
        Note note = entity.GetComponent<Note>();
        if (note.Kind != NoteKind.Rail)
            return;
        if (note.ChannelSide == ChannelSide.Left ? !shown.Left : !shown.Right)
            return;

        long delta = note.TimeMs - playhead.TimeMs;
        if (delta > 3_000 || delta < -600)
            return;

        float alpha = delta >= 0
            ? Math.Clamp(1f - delta / 3_000f, 0.12f, 1f)
            : Math.Clamp(1f + delta / 600f, 0f, 1f);
        if (alpha <= 0)
            return;

        float radius = SplitlineRail.NoteRadius * view.Scale;
        Rect2 field = FieldRect(view, playfield);
        Vector2 position = SplitlineRail.ScreenPosition(
            view,
            playfield,
            note,
            playhead.TimeMs);
        if (position.Y < field.Position.Y - radius * 2 ||
            position.Y > field.End.Y + radius * 2)
        {
            return;
        }

        Color color = note.HoldMs > 0
            ? Palette.InfoSoft
            : Palette.TextBright;
        color.A = alpha * (entity.Tags.Has<NoteMissed>() ? 0.35f : 1f);

        if (note.HoldMs > 0)
        {
            float length = note.HoldMs * SplitlineRail.PixelsPerMs * view.Scale;
            canvas.DrawLine(
                rid,
                position,
                position + new Vector2(0, length),
                new Color(color.R, color.G, color.B, color.A * 0.55f),
                radius * 0.55f);
        }

        canvas.DrawCircle(rid, position, radius, color);
        canvas.DrawTextStyled(
            rid,
            new Rect2(
                position - new Vector2(radius, radius),
                new Vector2(radius * 2, radius * 2)),
            ((int)note.PairIndex).ToString(),
            new Color(0.05f, 0.06f, 0.1f, color.A),
            Math.Max(9, (int)(radius * 1.1f)));
    }

    /// <summary>Two small trailing balls; purely an availability indicator.</summary>
    public static void DrawSplitlineBalls(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view)
    {
        foreach (Entity entity in entities.Query<Player>().ToEntityList())
        {
            if (entity.Tags.Has<SplitlineAbsent>())
                return;
        }

        foreach (Entity entity in entities.Query<SplitlineBalls>().ToEntityList())
        {
            if (!entity.TryGetComponent(out PlayerCharacter character) ||
                character.Kind != CharacterKind.SplitLine)
            {
                continue;
            }

            SplitlineBalls balls = entity.GetComponent<SplitlineBalls>();
            float radius = 7f * view.Scale;
            canvas.DrawCircle(
                rid,
                balls.Left,
                radius,
                new Color(0.13f, 0.83f, 0.93f, 0.85f));
            canvas.DrawCircle(
                rid,
                balls.Right,
                radius,
                new Color(0.93f, 0.28f, 0.6f, 0.85f));
        }
    }
}
