using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Canvas;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

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
        DrawNoteGuides(canvas, rid, entities, view, playhead, size / 2);

        foreach (Entity entity in entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            float alpha = NoteFade.Alpha(note, playhead.TimeMs);
            if (alpha <= 0)
                continue;

            string commonShader = ItemCommonShader.Of(entity);
            if (commonShader.Length > 0)
                canvas.SelectCommonShader(commonShader);
            else
                canvas.SelectShader(ItemShader.Of(entity));
            Vector2 center = view.PlayfieldToScreen(note.Position);
            float radius = size / 2;
            Color laneColor = GroupColor.For(entity, ColorFor(note));
            Color color = entity.Tags.Has<NoteMissed>()
                ? new Color(0.4f, 0.4f, 0.5f, alpha * laneColor.A * 0.45f)
                : new Color(laneColor.R, laneColor.G, laneColor.B, alpha * laneColor.A);

            float approach = NoteFade.Approach(note.TimeMs, playhead.TimeMs);
            if (approach >= 0)
            {
                float approachRadius =
                    ApproachRadius(radius, approach);
                canvas.DrawCircleOutline(
                    rid,
                    center,
                    approachRadius,
                    new Color(laneColor.R, laneColor.G, laneColor.B, alpha * laneColor.A * 0.75f),
                    2);
            }

            DrawNoteBody(canvas, rid, note, center, radius, color);

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
                        color.A),
                    2);
            }

            if (entity.TryGetComponent(out ItemGroups groups) && groups.Mask != 0)
            {
                DrawGroupBadge(
                    canvas,
                    rid,
                    center + new Vector2(radius * 0.78f, -radius * 0.78f),
                    groups.Mask,
                    color.A);
            }
        }

        canvas.SelectCommonShader("");
        canvas.SelectShader(0);
    }

    /// <summary>Draws just a note's body at a position, shared by play and the editor's group preview.</summary>
    public static void DrawNoteBody(
        IRectCanvas canvas,
        Rid rid,
        Note note,
        Vector2 center,
        float radius,
        Color color)
    {
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
                new Color(
                    CharacterSwitchColor.R,
                    CharacterSwitchColor.G,
                    CharacterSwitchColor.B,
                    CharacterSwitchColor.A * color.A),
                Math.Max(12, (int)(radius * 1.1f)));
        }
        else
        {
            canvas.DrawCircle(rid, center, radius, color);
        }
    }

    /// <summary>Guide lines in hit order, under the notes; simultaneous notes meet at a hub dot.</summary>
    private static void DrawNoteGuides(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Playhead playhead,
        float radius)
    {
        var points = new List<NoteGuides.Point>();
        foreach (Entity entity in entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            float alpha = NoteFade.Alpha(note, playhead.TimeMs);
            if (alpha > 0)
                points.Add(new NoteGuides.Point(note.TimeMs, view.PlayfieldToScreen(note.Position), alpha));
        }

        if (points.Count < 2)
            return;

        NoteGuides.Layout layout = NoteGuides.Build(points, radius);
        foreach (NoteGuides.Segment guide in layout.Guides)
            canvas.DrawLine(rid, guide.From, guide.To, Faded(GuideColor, guide.Alpha * 0.6f), 2);
        foreach (NoteGuides.Segment spoke in layout.Spokes)
            canvas.DrawLine(rid, spoke.From, spoke.To, Faded(GuideColor, spoke.Alpha * 0.8f), 2);
        foreach (NoteGuides.Hub hub in layout.Hubs)
            canvas.DrawCircle(rid, hub.Center, radius * NoteGuides.HubRadiusScale, Faded(GuideColor, hub.Alpha));
    }

    private static Color Faded(Color color, float alpha) => new(color.R, color.G, color.B, alpha);

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

            canvas.SelectShader(ItemShader.Of(entity));
            Vector2 center = view.PlayfieldToScreen(mine.Position);
            var bounds = new Rect2(
                center - new Vector2(size / 2, size / 2),
                new Vector2(size, size));
            Color color = GroupColor.For(entity, new Color(1f, 0.22f, 0.16f, alpha));

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

            if (entity.TryGetComponent(out ItemGroups groups) && groups.Mask != 0)
            {
                DrawGroupBadge(
                    canvas,
                    rid,
                    center + new Vector2(size * 0.55f, -size * 0.55f),
                    groups.Mask,
                    color.A);
            }
        }

        canvas.SelectShader(0);
    }

    public static Color ColorFor(Note note) =>
        note.Kind == NoteKind.CharacterSwitch
            ? CharacterSwitchColor
            : ColorFor(note.Target);

    public static Color ColorFor(KeybindAction action) =>
        LaneColors[(int)action % LaneColors.Length];
}
