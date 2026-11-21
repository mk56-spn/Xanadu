using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Systems.Canvas;
using Position = Xanadu.Core.Position;
using static Xanadu.Model.Playfields.PlayfieldColors;
using static Xanadu.Model.Playfields.PlayfieldMetrics;
using Xanadu.Model.Playfields;

namespace Xanadu.Drawers.Playfields;

public static partial class PlayfieldRenderer
{
    /// <summary>
    /// Draws one note: a flat rectangle in its key's colour, sliding down its
    /// key's line of the chord in charge at its time and resting on the
    /// judgement line from its time on (<see cref="Chords"/>). Greyed once missed;
    /// outlined while selected.
    /// </summary>
    public static void DrawNote(
        IRectCanvas canvas,
        Handle rid,
        Entity entity,
        PlayfieldView view,
        Playhead playhead,
        Entity selectedNote)
    {
        Note note = entity.GetComponent<Note>();
        double now = playhead.ShownMs;
        float alpha = Chords.NoteAlpha(note, now);
        if (alpha <= 0)
            return;

        Vector2 center = view.PlayfieldToScreen(Chords.NoteAt(entity.Store, note, now));
        Color lane = ColorFor(note);
        Color color = entity.Tags.Has<NoteMissed>()
            ? new Color(0.4f, 0.4f, 0.5f, alpha * lane.A * 0.45f)
            : new Color(lane.R, lane.G, lane.B, alpha * lane.A);
        bool selected = !selectedNote.IsNull && selectedNote.Id == entity.Id;
        DrawNoteShape(canvas, rid, note, center, view.Scale, color, selected);
    }

    /// <summary>A note's shape at a screen point: a key's rectangle, or a dash's bar across the whole chord.</summary>
    public static void DrawNoteShape(IRectCanvas canvas, Handle rid, in Note note, Vector2 center, float scale, Color color, bool selected = false)
    {
        if (note.IsDash)
            DrawDashBar(canvas, rid, center, scale, color, selected);
        else
            DrawNoteRect(canvas, rid, center, scale, color, selected);
    }

    /// <summary>
    /// A dash: a thin bar through every line of the chord, with a small
    /// diamond in the middle; it names no way, the player picks one.
    /// </summary>
    public static void DrawDashBar(IRectCanvas canvas, Handle rid, Vector2 center, float scale, Color color, bool selected = false)
    {
        Vector2 size = Chords.DashSize * scale;
        var rect = new Rect2(center - size / 2f, size);
        canvas.DrawRect(rid, rect, color);
        float r = MathF.Max(4f, 14f * scale);
        var edge = new Color(1f, 1f, 1f, color.A);
        canvas.DrawLine(rid, center + new Vector2(0, -r), center + new Vector2(r, 0), edge, 2f);
        canvas.DrawLine(rid, center + new Vector2(r, 0), center + new Vector2(0, r), edge, 2f);
        canvas.DrawLine(rid, center + new Vector2(0, r), center + new Vector2(-r, 0), edge, 2f);
        canvas.DrawLine(rid, center + new Vector2(-r, 0), center + new Vector2(0, -r), edge, 2f);
        if (selected)
            canvas.DrawBorder(rid, rect.Grow(4f), new Color(SelectedNoteColor.R, SelectedNoteColor.G, SelectedNoteColor.B, color.A), 2f);
    }

    /// <summary>A note's rectangle at a screen point, with a bright edge; a selection outline round it when asked.</summary>
    public static void DrawNoteRect(IRectCanvas canvas, Handle rid, Vector2 center, float scale, Color color, bool selected = false)
    {
        Vector2 size = Chords.NoteSize * scale;
        var rect = new Rect2(center - size / 2f, size);
        canvas.DrawRect(rid, rect, color);
        canvas.DrawBorder(rid, rect, new Color(1f, 1f, 1f, color.A * 0.8f), MathF.Max(1f, 2f * scale));
        if (selected)
            canvas.DrawBorder(rid, rect.Grow(3f), new Color(SelectedNoteColor.R, SelectedNoteColor.G, SelectedNoteColor.B, color.A), 2f);
    }

    /// <summary>
    /// Every chord showing at the playhead: its lines, one per action key in
    /// that key's colour, and its judgement line across the field. A chord
    /// fades in over the approach before its time.
    /// </summary>
    public static void DrawChords(IRectCanvas canvas, Handle rid, EntityStore entities, PlayfieldView view, Playfield playfield, double nowMs)
    {
        List<Chord> chords = Chords.All(entities, ChordScratch);
        foreach (Chord chord in chords)
        {
            (long from, long to) = Chords.Shown(chord, chords);
            if (nowMs < from || nowMs >= to)
                continue;

            float alpha = (float)Math.Clamp((nowMs - from) / 200d, 0d, 1d);
            Vector2 left = view.PlayfieldToScreen(new Vector2(0f, chord.Anchor.Y));
            Vector2 right = view.PlayfieldToScreen(new Vector2(playfield.Width, chord.Anchor.Y));
            canvas.DrawLine(rid, left, right, new Color(1f, 1f, 1f, alpha * 0.35f), 2f);
            for (int line = 0; line < Chords.LineCount; line++)
            {
                Color lane = ColorFor(Xanadu.Model.Settings.KeybindActions.ActionKeys[line]);
                Vector2 top = view.PlayfieldToScreen(Chords.Top(chord, line));
                Vector2 landing = view.PlayfieldToScreen(Chords.Landing(chord, line));
                DrawFadedLine(canvas, rid, top, landing, new Color(lane.R, lane.G, lane.B, alpha * 0.55f), 3f);
                canvas.DrawCircle(rid, landing, 5f, new Color(1f, 1f, 1f, alpha * 0.8f));
            }
        }
    }

    private static readonly List<Chord> ChordScratch = new();

    /// <summary>How much of a chord line, from its top, fades in from nothing to its full colour.</summary>
    private const float LineFadeShare = 0.45f;

    /// <summary>
    /// A chord line from <paramref name="top"/> to <paramref name="bottom"/>:
    /// clear at the top, reaching <paramref name="color"/> <see cref="LineFadeShare"/>
    /// of the way down and holding it to the bottom, so notes come in out of nothing.
    /// </summary>
    public static void DrawFadedLine(IRectCanvas canvas, Handle rid, Vector2 top, Vector2 bottom, Color color, float width)
    {
        Vector2 along = bottom - top;
        if (along.LengthSquared() < 1e-4f)
            return;

        Vector2 side = Vector2.Normalize(new Vector2(-along.Y, along.X)) * (width / 2f);
        Vector2 mid = top + along * LineFadeShare;
        Color clear = color with { A = 0f };
        Span<Vector2> points = stackalloc Vector2[12];
        Span<Color> colors = stackalloc Color[12];
        Quad(points, colors, 0, top, mid, clear, color);
        Quad(points, colors, 6, mid, bottom, color, color);
        canvas.DrawTriangles(rid, points, colors);

        void Quad(Span<Vector2> p, Span<Color> c, int at, Vector2 from, Vector2 to, Color fromColor, Color toColor)
        {
            p[at] = from - side; p[at + 1] = from + side; p[at + 2] = to + side;
            p[at + 3] = from - side; p[at + 4] = to + side; p[at + 5] = to - side;
            c[at] = fromColor; c[at + 1] = fromColor; c[at + 2] = toColor;
            c[at + 3] = fromColor; c[at + 4] = toColor; c[at + 5] = toColor;
        }
    }

    private static Color ApproachColor(Color lane, float alpha) => new(lane.R, lane.G, lane.B, alpha * lane.A * 0.75f);

    /// <summary>A wall note: a flat upright slab, meant to sit on a wall's face, with a bright core line.</summary>
    private static void DrawWallShape(IRectCanvas canvas, Handle rid, Vector2 center, float radius, Color color)
    {
        Vector2 size = WallNoteSize(radius);
        var slab = new Rect2(center - size / 2, size);
        canvas.DrawRect(rid, slab, color with { A = color.A * 0.55f });
        canvas.DrawBorder(rid, slab, color, Math.Max(1.5f, radius * 0.08f));
        canvas.DrawLine(rid, center - new Vector2(0, size.Y / 2 - radius * 0.2f), center + new Vector2(0, size.Y / 2 - radius * 0.2f), NoteEdgeColor with { A = color.A }, Math.Max(1f, radius * 0.1f));
    }

    /// <summary>Draws just a note's body at a position, shared by play and the editor's group preview.</summary>
    public static void DrawNoteBody(
        IRectCanvas canvas,
        Handle rid,
        Note note,
        Vector2 center,
        float radius,
        Color color)
    {
        DrawHitShape(canvas, rid, center, radius, color);
    }

    private static Color Faded(Color color, float alpha) => new(color.R, color.G, color.B, alpha);
}
