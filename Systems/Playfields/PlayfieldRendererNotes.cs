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
    /// <summary>
    /// Draws one note (not a rail note) with its approach ring, dash link,
    /// selection ring and group badge, under its own shader.
    /// </summary>
    public static void DrawNote(
        IRectCanvas canvas,
        Rid rid,
        Entity entity,
        PlayfieldView view,
        Playhead playhead,
        Entity selectedNote)
    {
        float size = NoteSize * view.Scale;
        Note note = entity.GetComponent<Note>();
        float alpha = NoteFade.Alpha(note, playhead.TimeMs);
        if (alpha <= 0)
            return;

        Vector2 center = view.PlayfieldToScreen(note.Position);
        float radius = size / 2;
        Color laneColor = GroupColor.For(entity, ColorFor(note));
        Color color = entity.Tags.Has<NoteMissed>()
            ? new Color(0.4f, 0.4f, 0.5f, alpha * laneColor.A * 0.45f)
            : new Color(laneColor.R, laneColor.G, laneColor.B, alpha * laneColor.A);

        float approach = NoteFade.Approach(note.TimeMs, playhead.TimeMs);
        float approachRadius = approach >= 0 ? ApproachRadius(radius, approach) : 0f;
        var ring = ApproachColor(laneColor, alpha);

        SelectItemShader(canvas, entity);

        if (approach >= 0)
            canvas.DrawCircleOutline(rid, center, approachRadius, ring, 2);

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


        canvas.SelectCommonShader("");
        canvas.SelectShader(0);
    }

    private static Color ApproachColor(Color lane, float alpha) => new(lane.R, lane.G, lane.B, alpha * lane.A * 0.75f);

    /// <summary>Draws just a note's body at a position, shared by play and the editor's group preview.</summary>
    public static void DrawNoteBody(
        IRectCanvas canvas,
        Rid rid,
        Note note,
        Vector2 center,
        float radius,
        Color color)
    {
        switch (note.Kind)
        {
            case NoteKind.Dash:
                DrawDashShape(canvas, rid, note, center, radius, color);
                break;
            case NoteKind.Momentum:
                DrawMomentumShape(canvas, rid, note, center, radius, color);
                break;
            case NoteKind.Contact:
                DrawBarrierShape(canvas, rid, note, center, radius, color);
                break;
            case NoteKind.Neutral:
                DrawNeutralShape(canvas, rid, center, radius, color);
                break;
            default:
                DrawHitShape(canvas, rid, center, radius, color);
                break;
        }
    }

    /// <summary>Guide lines in hit order, under the notes; simultaneous notes meet at a hub dot.</summary>
    public static void DrawNoteGuides(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Playhead playhead)
    {
        float radius = NoteSize * view.Scale / 2;
        var points = new List<NoteGuides.Point>();
        foreach (Entity entity in entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            float alpha = NoteFade.Alpha(note, playhead.TimeMs);
            if (alpha > 0)
                points.Add(new NoteGuides.Point(note.TimeMs, view.PlayfieldToScreen(GroupOffset.Shown(entity, note.Position)), alpha));
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

    /// <summary>Draws one mine while it shows, under its own shader.</summary>
    public static void DrawMine(
        IRectCanvas canvas,
        Rid rid,
        Entity entity,
        PlayfieldView view,
        Playhead playhead)
    {
        float size = MineSize * view.Scale;
        Mine mine = entity.GetComponent<Mine>();
        float alpha = MineFade.Alpha(mine, playhead.TimeMs);
        if (alpha <= 0)
            return;

        SelectItemShader(canvas, entity);
        Vector2 center = view.PlayfieldToScreen(mine.Position);
        var bounds = new Rect2(
            center - new Vector2(size / 2, size / 2),
            new Vector2(size, size));
        Color color = GroupColor.For(entity, new Color(1f, 0.22f, 0.16f, alpha));

        canvas.DrawRect(
            rid,
            bounds,
            new Color(0.08f, 0.02f, 0.02f, alpha * 0.9f));
        canvas.DrawBorder(rid, bounds, color, 2);
        canvas.DrawBorder(
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


        canvas.SelectShader(0);
    }

    public static Color ColorFor(Note note) =>
        note.Kind switch
        {
            NoteKind.Contact => ContactBarrierColor,
            NoteKind.Neutral => NeutralColor,
            NoteKind.Dash => DashColor,
            NoteKind.Momentum => MomentumColor,
            _ => ColorFor(note.Target),
        };

    /// <summary>The colour of an action key's notes and Contact bar; direction keys get the neutral colour.</summary>
    public static Color ColorFor(KeybindAction action)
    {
        int bar = ContactCounters.BarOf(action);
        return bar >= 0 ? ActionColors[bar] : NeutralColor;
    }

    /// <summary>The colour of Contact bar 0, 1 or 2: its action key's.</summary>
    public static Color BarColor(int bar) => ActionColors[bar];
}
