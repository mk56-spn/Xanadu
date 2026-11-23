using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Geometry;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Logic.Editor.Kinds;

/// <summary>
/// Notes: timed, pasted at the playhead. A note on a chord sits on its key's
/// line, so it is picked where it is drawn at the playhead and is moved by
/// moving its chord, or by retiming it; its own position only counts without one.
/// </summary>
public sealed class NoteItemKind : ItemKind<Note, NoteDocument>
{
    private const float HitRadius = PlayfieldMetrics.NoteSize / 2;

    public override string Name => "note";

    public override Vector2 Position(Entity entity) => Chords.Landing(entity.Store, Get(entity));

    public override void SetPosition(Entity entity, Vector2 position)
    {
        Ref(entity).Position = position;
    }

    public override Rect2 Hull(Entity entity) => new(Position(entity), Vector2.Zero);

    public override long? Time(Entity entity) => Get(entity).TimeMs;

    public override bool SetTime(Entity entity, long timeMs)
    {
        Ref(entity).TimeMs = timeMs;
        return true;
    }

    public override long? TimeOf(object value) => ((Note)value).TimeMs;

    public override bool Hit(Entity entity, Vector2 point, long playheadMs)
    {
        Note note = Get(entity);
        // An entrance shows wherever it was placed, at any time.
        if (note.IsEntrance)
            return TimedKinds.Within(point, note.Position, PlayRules.BodyHeight * 0.8f);
        // An orb is picked at its start, or where it is at the playhead.
        if (note.IsOrb)
            return TimedKinds.Within(point, note.Position, Orbs.Radius * 1.6f) ||
                   TimedKinds.Within(point, Orbs.At(note, playheadMs), Orbs.Radius * 1.6f);
        if (Chords.NoteAlpha(note, playheadMs) <= 0)
            return false;

        Vector2 at = Chords.NoteAt(entity.Store, note, playheadMs);
        if (!note.IsDash)
            return TimedKinds.Within(point, at, HitRadius);

        // A dash is picked anywhere along its bar.
        Vector2 half = Chords.DashSize / 2f;
        return MathF.Abs(point.X - at.X) <= half.X && MathF.Abs(point.Y - at.Y) <= MathF.Max(half.Y, HitRadius);
    }

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs)
    {
        Note note = Get(entity);
        Vector2 at = Chords.NoteAt(entity.Store, note, playheadMs);
        if ((!note.IsEntrance && !note.IsOrb && Chords.NoteAlpha(note, playheadMs) <= 0.05f) || !PlayfieldGrid.IsInside(at, playfield))
            return false;

        return screen.Intersects(TimedKinds.Square(entity, at, PlayfieldMetrics.NoteSize, view));
    }

    protected override int Compare(in Note a, in Note b)
    {
        int order = ByTimeThenPosition(a.TimeMs, a.Position, b.TimeMs, b.Position);
        if (order != 0)
            return order;
        order = a.Kind.CompareTo(b.Kind);
        return order != 0 ? order : a.Target.CompareTo(b.Target);
    }

    protected override Note? Pasted(Note value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities) =>
        value with { TimeMs = timeMs };

    protected override List<NoteDocument> List(LevelDocument level) => level.Notes;

    protected override int CompareDocuments(NoteDocument a, NoteDocument b) => a.TimeMs.CompareTo(b.TimeMs);

    protected override NoteDocument ToDocument(in Note note) => NoteDocument.From(note);

    protected override Note FromDocument(NoteDocument note) => note.ToNote();
}

/// <summary>
/// Chords: timed, picked on any of their lines while they show, moved by their
/// anchor (where the first line meets the judgement line), pasted at the playhead.
/// </summary>
public sealed class ChordItemKind : ItemKind<Chord, ChordDocument>
{
    private const float HitRadius = 14f;

    private readonly List<Chord> _sorted = [];

    public override string Name => "chord";

    public override Vector2 Position(Entity entity) => Get(entity).Anchor;

    public override void SetPosition(Entity entity, Vector2 position)
    {
        Ref(entity).Anchor = position;
    }

    public override Rect2 Hull(Entity entity)
    {
        (Vector2 min, Vector2 max) = Chords.Bounds(Get(entity));
        return new Rect2(min, max - min);
    }

    public override long? Time(Entity entity) => Get(entity).TimeMs;

    public override bool SetTime(Entity entity, long timeMs)
    {
        Ref(entity).TimeMs = timeMs;
        return true;
    }

    public override long? TimeOf(object value) => ((Chord)value).TimeMs;

    private bool Showing(Entity entity, long playheadMs)
    {
        (long from, long to) = Chords.Shown(Get(entity), Chords.All(entity.Store, _sorted));
        return playheadMs >= from && playheadMs < to;
    }

    public override bool Hit(Entity entity, Vector2 point, long playheadMs)
    {
        if (!Showing(entity, playheadMs))
            return false;

        Chord chord = Get(entity);
        for (int line = 0; line < Chords.LineCount; line++)
        {
            if (Splines.SegmentDistance(point, Chords.Top(chord, line), Chords.Landing(chord, line)) <= HitRadius)
                return true;
        }

        return false;
    }

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs)
    {
        if (!Showing(entity, playheadMs))
            return false;

        Vector2 anchor = view.PlayfieldToScreen(Get(entity).Anchor);
        return screen.HasPoint(anchor);
    }

    protected override int Compare(in Chord a, in Chord b) =>
        ByTimeThenPosition(a.TimeMs, a.Anchor, b.TimeMs, b.Anchor);

    protected override Chord? Pasted(Chord value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities) =>
        value with { TimeMs = timeMs };

    protected override List<ChordDocument> List(LevelDocument level) => level.Chords;

    protected override int CompareDocuments(ChordDocument a, ChordDocument b) => a.TimeMs.CompareTo(b.TimeMs);

    protected override ChordDocument ToDocument(in Chord chord) => ChordDocument.From(chord);

    protected override Chord FromDocument(ChordDocument chord) => chord.ToChord();
}

/// <summary>Helpers the timed kinds share.</summary>
internal static class TimedKinds
{
    /// <summary>Whether a point lies within a radius in both axes of an item's centre.</summary>
    public static bool Within(Vector2 point, Vector2 target, float radius) =>
        MathF.Abs(target.X - point.X) <= radius &&
        MathF.Abs(target.Y - point.Y) <= radius;

    /// <summary>The screen square an item of <paramref name="size"/> covers where its groups show it.</summary>
    public static Rect2 Square(Entity entity, Vector2 position, float size, PlayfieldView view)
    {
        float side = size * view.Scale;
        Vector2 center = view.PlayfieldToScreen(position);
        return new Rect2(center - new Vector2(side / 2, side / 2), new Vector2(side, side));
    }
}
