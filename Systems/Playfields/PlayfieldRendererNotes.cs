using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
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
            if (NoteRules.IsRail(note.Kind) || NoteRules.IsChannelEnd(note.Kind))
                continue;

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
        if (note.Kind == NoteKind.Dash)
        {
            int strength = DashStrengthLevel(note.DashStrength);
            canvas.DrawCircleOutline(rid, center, radius, color, 1 + strength);
            canvas.DrawTextStyled(
                rid,
                new Rect2(
                    center - new Vector2(radius, radius),
                    new Vector2(radius * 2, radius * 2)),
                DashArrow(note.DashDirection),
                color,
                Math.Max(10, (int)(radius * 1.1f)));

            if (strength > 1)
            {
                Vector2 badgeCenter = center + new Vector2(radius * 0.55f, radius * 0.55f);
                canvas.DrawCircle(rid, badgeCenter, radius * 0.34f,
                    new Color(0f, 0f, 0f, color.A * 0.55f));
                canvas.DrawTextStyled(
                    rid,
                    new Rect2(
                        badgeCenter - new Vector2(radius * 0.45f, radius * 0.45f),
                        new Vector2(radius * 0.9f, radius * 0.9f)),
                    DashStrengthLabel(note.DashStrength),
                    color,
                    Math.Max(8, (int)(radius * 0.55f)));
            }
        }
        else if (note.Kind == NoteKind.Momentum)
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

            Vector2 badgeCenter = center + new Vector2(radius * 0.52f, radius * 0.52f);
            canvas.DrawCircle(rid, badgeCenter, radius * 0.32f,
                new Color(0f, 0f, 0f, color.A * 0.55f));
            canvas.DrawTextStyled(
                rid,
                new Rect2(
                    badgeCenter - new Vector2(radius * 0.4f, radius * 0.4f),
                    new Vector2(radius * 0.8f, radius * 0.8f)),
                "M",
                color,
                Math.Max(8, (int)(radius * 0.5f)));
        }
        else if (note.Kind == NoteKind.Pair)
        {
            canvas.DrawCircleOutline(rid, center, radius, color, 2);
            canvas.DrawTextStyled(
                rid,
                new Rect2(
                    center - new Vector2(radius, radius),
                    new Vector2(radius * 2, radius * 2)),
                ((int)note.PairIndex).ToString(),
                color,
                Math.Max(12, (int)(radius * 1.1f)));

            // Two side ticks: the number is what matters, not which hand.
            float tickX = radius * 0.78f;
            float tickY = radius * 0.34f;
            canvas.DrawLine(
                rid,
                center + new Vector2(-tickX, -tickY),
                center + new Vector2(-tickX, tickY),
                color,
                2);
            canvas.DrawLine(
                rid,
                center + new Vector2(tickX, -tickY),
                center + new Vector2(tickX, tickY),
                color,
                2);
        }
        else if (note.Kind == NoteKind.Contact)
        {
            (int left, int right) = ContactRules.Costs(note);
            canvas.DrawCircleOutline(rid, center, radius, color, 3);
            canvas.DrawTextStyled(
                rid,
                new Rect2(
                    center - new Vector2(radius, radius * 0.55f),
                    new Vector2(radius * 2, radius * 1.1f)),
                "C",
                color,
                Math.Max(12, (int)(radius * 0.9f)));

            if (left > 0 || right > 0)
            {
                canvas.DrawTextStyled(
                    rid,
                    new Rect2(
                        center + new Vector2(-radius, radius * 0.28f),
                        new Vector2(radius * 2, radius * 0.5f)),
                    $"L{left} R{right}",
                    color,
                    Math.Max(8, (int)(radius * 0.4f)));
            }
        }
        else if (note.Kind == NoteKind.WeaponSwitch)
        {
            canvas.DrawCircleOutline(rid, center, radius, color, 3);
            canvas.DrawTextStyled(
                rid,
                new Rect2(
                    center - new Vector2(radius, radius),
                    new Vector2(radius * 2, radius * 2)),
                "W",
                color,
                Math.Max(12, (int)(radius * 1.1f)));
        }
        else if (note.Kind == NoteKind.Channel)
        {
            canvas.DrawCircleOutline(rid, center, radius, color, 3);
            canvas.DrawTextStyled(
                rid,
                new Rect2(
                    center - new Vector2(radius, radius),
                    new Vector2(radius * 2, radius * 2)),
                NoteRules.IsLeft(note.Target) ? "L" : "R",
                color,
                Math.Max(12, (int)(radius * 1.0f)));
        }
        else if (note.Kind == NoteKind.Rail)
        {
            canvas.DrawCircleOutline(rid, center, radius, color, 2);
            canvas.DrawTextStyled(
                rid,
                new Rect2(
                    center - new Vector2(radius, radius),
                    new Vector2(radius * 2, radius * 2)),
                ((int)note.PairIndex).ToString(),
                color,
                Math.Max(10, (int)(radius * 0.9f)));
        }
        else if (note.Kind == NoteKind.ChannelEnd)
        {
            // Chart data only; the rail preview draws the end.
        }
        else if (note.Kind == NoteKind.Construction)
        {
            canvas.DrawCircleOutline(rid, center, radius, color, 3);
            canvas.DrawTextStyled(
                rid,
                new Rect2(
                    center - new Vector2(radius, radius),
                    new Vector2(radius * 2, radius * 2)),
                "F",
                color,
                Math.Max(12, (int)(radius * 1.0f)));
        }
        else if (note.Kind == NoteKind.ConstructionStep)
        {
            canvas.DrawCircleOutline(rid, center, radius, color, 2);
            canvas.DrawTextStyled(
                rid,
                new Rect2(
                    center - new Vector2(radius, radius),
                    new Vector2(radius * 2, radius * 2)),
                note.ShapePiece.ToString(),
                color,
                Math.Max(10, (int)(radius * 0.9f)));
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
            if (NoteRules.IsRail(note.Kind) || NoteRules.IsChannelEnd(note.Kind))
                continue;

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

    /// <summary>One, two or three dash lengths.</summary>
    public static int DashStrengthLevel(DashStrength strength) =>
        strength switch
        {
            DashStrength.Double => 2,
            DashStrength.Triple => 3,
            _ => 1,
        };

    /// <summary>The small badge that marks a double or triple dash; empty for a single.</summary>
    public static string DashStrengthLabel(DashStrength strength) =>
        strength switch
        {
            DashStrength.Double => "×2",
            DashStrength.Triple => "×3",
            _ => "",
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
        note.Kind switch
        {
            NoteKind.CharacterSwitch => CharacterSwitchColor,
            NoteKind.Pair => PairColor,
            NoteKind.Dash => DashColor,
            NoteKind.Momentum => MomentumColor,
            NoteKind.Contact => ContactBarrierColor,
            NoteKind.WeaponSwitch => WeaponSwitchColor,
            NoteKind.Channel => ChannelColor,
            NoteKind.Rail => RailColor,
            NoteKind.ChannelEnd => Palette.TextDim,
            NoteKind.Construction => ConstructionColor,
            NoteKind.ConstructionStep => ConstructionStepColor,
            _ => ColorFor(note.Target),
        };

    public static Color ColorFor(KeybindAction action) =>
        LaneColors[(int)action % LaneColors.Length];
}
