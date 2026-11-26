using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;

namespace Xanadu.Drawers.Playfields;

public static partial class PlayfieldRenderer
{
    /// <summary>
    /// Adds one note to <paramref name="copies"/>: a flat rectangle in its key's colour, sliding down its
    /// key's line of the chord in charge at its time and resting on the
    /// judgement line from its time on (<see cref="Chords"/>). Greyed once missed;
    /// outlined while selected (<see cref="NoteOutlines"/>, drawn over the notes).
    /// </summary>
    public static void DrawNote(
        NoteCopies copies,
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
        DrawNoteShape(copies, note, center, view.Scale, color);
        if (!selectedNote.IsNull && selectedNote.Id == entity.Id)
            NoteOutlines.Add((NoteOutline(note, center, view.Scale), new Color(SelectedNoteColor.R, SelectedNoteColor.G, SelectedNoteColor.B, color.A)));
    }

    /// <summary>Selection outlines round notes added this frame, drawn once the notes are.</summary>
    private static readonly List<(Rect2 Box, Color Color)> NoteOutlines = [];

    /// <summary>Draws the selection outlines collected with the notes, over them.</summary>
    private static void DrawNoteOutlines(IRectCanvas canvas, Handle rid)
    {
        foreach ((Rect2 box, Color color) in NoteOutlines)
            canvas.DrawBorder(rid, box, color, 2f);
        NoteOutlines.Clear();
    }

    /// <summary>A selected note's outline: round a key's rectangle, or a dash's bar.</summary>
    private static Rect2 NoteOutline(in Note note, Vector2 center, float scale)
    {
        Vector2 size = (note.IsDash ? Chords.DashSize : Chords.NoteSize) * scale;
        return new Rect2(center - size / 2f, size).Grow(note.IsDash ? 4f : 3f);
    }

    /// <summary>A note's shape at a screen point: a key's rectangle, or a dash's bar across the whole chord (it names no way, the player picks one).</summary>
    public static void DrawNoteShape(NoteCopies copies, in Note note, Vector2 center, float scale, Color color)
    {
        if (note.IsDash)
            copies.AddDash(center, scale, color);
        else
            copies.AddKey(center, scale, color);
    }

    /// <summary>
    /// Every chord showing at the playhead: its lines, one per action key in
    /// that key's colour, and its judgement line across the field, in one draw. A chord
    /// fades in over the approach before its time.
    /// </summary>
    public static void DrawChords(IRectCanvas canvas, Handle rid, EntityStore entities, PlayfieldView view, Playfield playfield, double nowMs)
    {
        NoteCopies copies = NoteCopies.For(rid, "chords");
        List<Chord> chords = Chords.All(entities, ChordScratch);
        foreach (Chord chord in chords)
        {
            (long from, long to) = Chords.Shown(chord, chords);
            if (nowMs < from || nowMs >= to)
                continue;

            float alpha = (float)Math.Clamp((nowMs - from) / 200d, 0d, 1d);
            Vector2 left = view.PlayfieldToScreen(new Vector2(0f, chord.Anchor.Y));
            Vector2 right = view.PlayfieldToScreen(new Vector2(playfield.Width, chord.Anchor.Y));
            copies.AddLine(left, right, new Color(1f, 1f, 1f, alpha * 0.35f), 2f, 0f);
            for (int line = 0; line < Chords.LineCount; line++)
            {
                Color lane = ColorFor(Xanadu.Model.Settings.KeybindActions.ActionKeys[line]);
                Vector2 top = view.PlayfieldToScreen(Chords.Top(chord, line));
                Vector2 landing = view.PlayfieldToScreen(Chords.Landing(chord, line));
                DrawFadedLine(copies, top, landing, new Color(lane.R, lane.G, lane.B, alpha * 0.55f), 3f);
                copies.AddDot(landing, 5f, new Color(1f, 1f, 1f, alpha * 0.8f));
            }
        }
        copies.Draw(canvas, rid, PlayfieldMetrics.FieldRect(view, playfield));
    }

    private static readonly List<Chord> ChordScratch = [];

    /// <summary>How much of a chord line, from its top, fades in from nothing to its full colour.</summary>
    private const float LineFadeShare = 0.45f;

    /// <summary>A chord line from <paramref name="top"/> to <paramref name="bottom"/>, clear at the top and full colour from <see cref="LineFadeShare"/> of the way down.</summary>
    public static void DrawFadedLine(NoteCopies copies, Vector2 top, Vector2 bottom, Color color, float width) =>
        copies.AddLine(top, bottom, color, width, LineFadeShare);
}
