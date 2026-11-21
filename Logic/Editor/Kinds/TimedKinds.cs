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
/// Notes: timed, picked by a square the note's size, pasted at the playhead.
/// A route note is also picked anywhere near its straight-ahead preview.
/// </summary>
public sealed class NoteItemKind : ItemKind<Note, NoteDocument>
{
    private const float HitRadius = PlayfieldMetrics.NoteSize / 2;

    public override string Name => "note";

    public override Vector2 Position(Entity entity) => Get(entity).Position;

    public override void SetPosition(Entity entity, Vector2 position)
    {
        Ref(entity).Position = position;
    }

    public override Rect2 Hull(Entity entity)
    {
        Note note = Get(entity);
        if (note.IsRoute)
            return RoutePath.Of(note).Bounds;
        if (note.IsHold)
        {
            Vector2 min = Vector2.Min(note.Position, note.TailPosition);
            return new Rect2(min, Vector2.Max(note.Position, note.TailPosition) - min);
        }

        return new Rect2(note.Position, Vector2.Zero);
    }

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
        if (note.IsRoute)
        {
            RoutePath path = RoutePath.Of(note);
            return RouteFade.Alpha(path, playheadMs) > 0 &&
                   (TimedKinds.Within(point, note.Position, HitRadius) || path.DistanceTo(point) <= HitRadius);
        }

        if (note.IsHold)
        {
            return NoteSpanFade.Alpha(note, playheadMs) > 0 &&
                   Splines.SegmentDistance(point, note.Position, note.TailPosition) <= HitRadius;
        }

        return NoteFade.Alpha(note, playheadMs) > 0 && TimedKinds.Within(point, note.Position, HitRadius);
    }

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs)
    {
        Note note = Get(entity);
        if (NoteFade.Alpha(note, playheadMs) <= 0.05f ||
            !PlayfieldGrid.IsInside(note.Position, playfield))
        {
            return false;
        }

        return screen.Intersects(TimedKinds.Square(entity, note.Position, PlayfieldMetrics.NoteSize, view));
    }

    protected override int Compare(in Note a, in Note b)
    {
        int order = ByTimeThenPosition(a.TimeMs, a.Position, b.TimeMs, b.Position);
        if (order != 0)
            return order;
        if ((order = a.Target.CompareTo(b.Target)) != 0)
            return order;
        if ((order = NoteRoute.Compare(a.Route, b.Route)) != 0)
            return order;
        if ((order = a.DashLength.CompareTo(b.DashLength)) != 0)
            return order;
        if ((order = (a.Hold?.TailMs ?? 0).CompareTo(b.Hold?.TailMs ?? 0)) != 0)
            return order;
        if ((order = (a.Hold?.Tail.X ?? 0f).CompareTo(b.Hold?.Tail.X ?? 0f)) != 0)
            return order;
        return (a.Hold?.Tail.Y ?? 0f).CompareTo(b.Hold?.Tail.Y ?? 0f);
    }

    protected override Note? Pasted(Note value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities) =>
        value with { TimeMs = timeMs };

    protected override List<NoteDocument> List(LevelDocument level) => level.Notes;

    protected override int CompareDocuments(NoteDocument a, NoteDocument b) => a.TimeMs.CompareTo(b.TimeMs);

    protected override NoteDocument ToDocument(in Note note) => NoteDocument.From(note);

    protected override Note FromDocument(NoteDocument note) => note.ToNote();
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
