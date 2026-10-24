using System;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Settings;
using Writer.Systems.Canvas;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Playfields;

public static partial class PlayfieldRenderer
{
    public static void DrawNotes(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Playfield playfield,
        Playhead playhead,
        Entity selectedNote)
    {
        float size = NoteSize * view.Scale;

        foreach (Entity entity in entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            float alpha = NoteFade.Alpha(note, playhead.TimeMs);
            if (alpha <= 0)
                continue;

            Vector2 center = view.PlayfieldToScreen(note.Position);
            float radius = size / 2;
            Color laneColor = ColorFor(note.Target);
            Color color = entity.Tags.Has<NoteMissed>()
                ? new Color(0.4f, 0.4f, 0.5f, alpha * 0.45f)
                : new Color(laneColor.R, laneColor.G, laneColor.B, alpha);

            float approach = NoteFade.Approach(note.TimeMs, playhead.TimeMs);
            if (approach >= 0)
            {
                float approachRadius =
                    ApproachRadius(radius, approach);
                canvas.DrawCircleOutline(
                    rid,
                    center,
                    approachRadius,
                    new Color(laneColor.R, laneColor.G, laneColor.B, alpha * 0.75f),
                    2);
            }

            if (note.Kind == NoteKind.Movement)
            {
                canvas.DrawCircleOutline(rid, center, radius, color, 2);
                canvas.DrawTextStyled(
                    rid,
                    new Rect2(
                        center - new Vector2(radius, radius),
                        new Vector2(radius * 2, radius * 2)),
                    DashArrow(note.DashDirection),
                    color,
                    Math.Max(10, (int)(radius * 1.1f)));
            }
            else if (note.Kind == NoteKind.Contact)
            {
                canvas.DrawCircleOutline(rid, center, radius, color, 3);
                canvas.DrawTextStyled(
                    rid,
                    new Rect2(
                        center - new Vector2(radius, radius),
                        new Vector2(radius * 2, radius * 2)),
                    "C",
                    color,
                    Math.Max(12, (int)(radius * 1.1f)));
            }
            else if (note.Kind == NoteKind.CharacterSwitch)
            {
                canvas.DrawCircleOutline(rid, center, radius, color, 3);
                canvas.DrawTextStyled(
                    rid,
                    new Rect2(
                        center - new Vector2(radius, radius),
                        new Vector2(radius * 2, radius * 2)),
                    note.Character.ToString()[..1],
                    color,
                    Math.Max(12, (int)(radius * 1.1f)));
            }
            else
            {
                canvas.DrawCircle(rid, center, radius, color);
            }

            if (!selectedNote.IsNull && selectedNote.Id == entity.Id)
            {
                canvas.DrawCircleOutline(
                    rid,
                    center,
                    radius + 1.5f,
                    new Color(
                        SelectedNoteColor.R,
                        SelectedNoteColor.G,
                        SelectedNoteColor.B,
                        alpha),
                    2);
            }
        }
    }

    /// <summary>Radius of the approach ring at a 0..1 approach amount.</summary>
    public static float ApproachRadius(float noteRadius, float approach) =>
        noteRadius * (1f + Math.Clamp(approach, 0f, 1f) * 8f);

    public static string DashArrow(DashDirection direction) =>
        direction switch
        {
            DashDirection.Up => "↑",
            DashDirection.UpRight => "↗",
            DashDirection.Right => "→",
            DashDirection.DownRight => "↘",
            DashDirection.Down => "↓",
            DashDirection.DownLeft => "↙",
            DashDirection.Left => "←",
            _ => "↖",
        };

    public static void DrawMines(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Playhead playhead)
    {
        float size = MineSize * view.Scale;

        foreach (Entity entity in entities.Query<Mine>().ToEntityList())
        {
            Mine mine = entity.GetComponent<Mine>();
            float alpha = MineFade.Alpha(mine, playhead.TimeMs);
            if (alpha <= 0)
                continue;

            Vector2 center = view.PlayfieldToScreen(mine.Position);
            var bounds = new Rect2(
                center - new Vector2(size / 2, size / 2),
                new Vector2(size, size));
            Color color = new Color(1f, 0.22f, 0.16f, alpha);

            canvas.DrawRect(
                rid,
                bounds,
                new Color(0.08f, 0.02f, 0.02f, alpha * 0.9f));
            DrawBorder(canvas, rid, bounds, color, 2);
            DrawBorder(
                canvas,
                rid,
                new Rect2(
                    bounds.Position + new Vector2(3, 3),
                    bounds.Size - new Vector2(6, 6)),
                new Color(1f, 0.55f, 0.2f, alpha),
                1);
            canvas.DrawLine(
                rid,
                bounds.Position + new Vector2(5, 5),
                bounds.End - new Vector2(5, 5),
                color,
                2);
            canvas.DrawLine(
                rid,
                new Vector2(bounds.End.X - 5, bounds.Position.Y + 5),
                new Vector2(bounds.Position.X + 5, bounds.End.Y - 5),
                color,
                2);
        }
    }

    public static Color ColorFor(KeybindAction action) =>
        LaneColors[(int)action % LaneColors.Length];
}
