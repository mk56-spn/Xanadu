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
    /// diamond in the middle; it names no way, the player picks one. Its look is
    /// <see cref="NoteLooks.Dash"/>, drawn by the Xif drawer; outlined when selected.
    /// </summary>
    public static void DrawDashBar(IRectCanvas canvas, Handle rid, Vector2 center, float scale, Color color, bool selected = false)
    {
        DrawNoteLook(canvas, rid, NoteLooks.Dash, center, scale, color);
        if (selected)
        {
            Vector2 size = Chords.DashSize * scale;
            canvas.DrawBorder(rid, new Rect2(center - size / 2f, size).Grow(4f), new Color(SelectedNoteColor.R, SelectedNoteColor.G, SelectedNoteColor.B, color.A), 2f);
        }
    }

    /// <summary>A note's rectangle at a screen point, with a bright edge (<see cref="NoteLooks.Key"/>); a selection outline round it when asked.</summary>
    public static void DrawNoteRect(IRectCanvas canvas, Handle rid, Vector2 center, float scale, Color color, bool selected = false)
    {
        DrawNoteLook(canvas, rid, NoteLooks.Key, center, scale, color);
        if (selected)
        {
            Vector2 size = Chords.NoteSize * scale;
            canvas.DrawBorder(rid, new Rect2(center - size / 2f, size).Grow(3f), new Color(SelectedNoteColor.R, SelectedNoteColor.G, SelectedNoteColor.B, color.A), 2f);
        }
    }

    /// <summary>A note look in <paramref name="color"/>, centred on <paramref name="center"/> at <paramref name="scale"/> screen pixels a unit.</summary>
    private static void DrawNoteLook(IRectCanvas canvas, Handle rid, Xanadu.Core.Items.Xif look, Vector2 center, float scale, Color color)
    {
        NoteLooks.SetColour(look, color);
        var placement = new Transform2D(new Vector2(scale, 0f), new Vector2(0f, scale), center);
        Xanadu.Model.Animation.XifDrawer.Shared.DrawNode(canvas, rid, look, placement, scale, Xanadu.Model.Animation.XifTint.Plain);
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
            NoteLooks.SetFade(0f);
            DrawChordLine(canvas, rid, left, right, new Color(1f, 1f, 1f, alpha * 0.35f), 2f);
            for (int line = 0; line < Chords.LineCount; line++)
            {
                Color lane = ColorFor(Xanadu.Model.Settings.KeybindActions.ActionKeys[line]);
                Vector2 top = view.PlayfieldToScreen(Chords.Top(chord, line));
                Vector2 landing = view.PlayfieldToScreen(Chords.Landing(chord, line));
                DrawFadedLine(canvas, rid, top, landing, new Color(lane.R, lane.G, lane.B, alpha * 0.55f), 3f);
                NoteLooks.SetColour(NoteLooks.Dot, new Color(1f, 1f, 1f, alpha * 0.8f));
                var dot = new Transform2D(new Vector2(5f, 0f), new Vector2(0f, 5f), landing);
                Xanadu.Model.Animation.XifDrawer.Shared.DrawNode(canvas, rid, NoteLooks.Dot, dot, 1f, Xanadu.Model.Animation.XifTint.Plain);
            }
        }
    }

    private static readonly List<Chord> ChordScratch = [];

    /// <summary>How much of a chord line, from its top, fades in from nothing to its full colour.</summary>
    private const float LineFadeShare = 0.45f;

    /// <summary>A chord line from <paramref name="top"/> to <paramref name="bottom"/>, clear at the top and full colour from <see cref="LineFadeShare"/> of the way down.</summary>
    public static void DrawFadedLine(IRectCanvas canvas, Handle rid, Vector2 top, Vector2 bottom, Color color, float width)
    {
        NoteLooks.SetFade(LineFadeShare);
        DrawChordLine(canvas, rid, top, bottom, color, width);
    }

    /// <summary>
    /// A chord line from <paramref name="from"/> to <paramref name="to"/>,
    /// <paramref name="width"/> pixels wide (<see cref="NoteLooks.Line"/>, fading
    /// in as last set with <see cref="NoteLooks.SetFade"/>), drawn by the Xif drawer.
    /// </summary>
    private static void DrawChordLine(IRectCanvas canvas, Handle rid, Vector2 from, Vector2 to, Color color, float width)
    {
        Vector2 along = to - from;
        if (along.LengthSquared() < 1e-4f)
            return;

        Vector2 side = Vector2.Normalize(new Vector2(-along.Y, along.X)) * (width / 2f);
        NoteLooks.SetColour(NoteLooks.Line, color);
        var placement = new Transform2D(along, side, from);
        Xanadu.Model.Animation.XifDrawer.Shared.DrawNode(canvas, rid, NoteLooks.Line, placement, 1f, Xanadu.Model.Animation.XifTint.Plain);
    }

    private static Color Faded(Color color, float alpha) => new(color.R, color.G, color.B, alpha);
}
