using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Items;
using Xanadu.Levels;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Playfields;

namespace Xanadu.Ecs;

/// <summary>Notes: timed, picked by a square the note's size, pasted at the playhead.</summary>
public sealed class NoteItemKind : ItemKind<Note, NoteDocument>
{
    private const float HitRadius = PlayfieldRenderer.NoteSize / 2;

    public override string Name => "note";

    public override Vector2 Position(Entity entity) => Get(entity).Position;

    public override void SetPosition(Entity entity, Vector2 position) => Ref(entity).Position = position;

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
        return NoteFade.Alpha(note, playheadMs) > 0 && TimedKinds.Within(point, note.Position, HitRadius);
    }

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs)
    {
        Note note = Get(entity);
        if (NoteFade.Alpha(note, playheadMs) <= 0.05f ||
            !EditorItemPicker.IsInside(note.Position, playfield))
        {
            return false;
        }

        return screen.Intersects(TimedKinds.Square(entity, note.Position, PlayfieldRenderer.NoteSize, view));
    }

    protected override int Compare(in Note a, in Note b)
    {
        int order = ByTimeThenPosition(a.TimeMs, a.Position, b.TimeMs, b.Position);
        if (order != 0)
            return order;
        if ((order = a.Kind.CompareTo(b.Kind)) != 0)
            return order;
        if ((order = a.Target.CompareTo(b.Target)) != 0)
            return order;
        if ((order = a.DashStrength.CompareTo(b.DashStrength)) != 0)
            return order;
        if ((order = a.Momentum.CompareTo(b.Momentum)) != 0)
            return order;
        if ((order = a.Diagonal.CompareTo(b.Diagonal)) != 0)
            return order;
        for (int bar = 0; bar < ContactCounters.Bars; bar++)
        {
            if ((order = a.Costs[bar].CompareTo(b.Costs[bar])) != 0)
                return order;
        }
        return 0;
    }

    protected override Note? Pasted(Note value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities) =>
        value with { TimeMs = timeMs };

    protected override List<NoteDocument> List(IChartItems chart) => chart.Notes ??= new();

    protected override int CompareDocuments(NoteDocument a, NoteDocument b) => a.TimeMs.CompareTo(b.TimeMs);

    protected override NoteDocument ToDocument(in Note note, Entity entity) => new()
    {
        TimeMs = note.TimeMs,
        X = note.Position.X,
        Y = note.Position.Y,
        Target = note.Target,
        Kind = note.Kind,
        DashStrength = note.DashStrength,
        Momentum = note.Momentum,
        Diagonal = note.Diagonal,
        CostFirst = note.Costs.First,
        CostSecond = note.Costs.Second,
        CostThird = note.Costs.Third,
        Item = Xif.FromEcs(ItemShader.Of(entity), ItemCommonShader.Of(entity)),
    };

    protected override Note FromDocument(NoteDocument note) => new(
        note.TimeMs,
        new Vector2(note.X, note.Y),
        note.Target,
        note.Kind,
        note.DashStrength,
        note.Momentum,
        note.Diagonal,
        new BarCosts(note.CostFirst, note.CostSecond, note.CostThird));

    /// <summary>A note's shaders live in its Xif: a map slot, a common shader, or both.</summary>
    protected override Entity CreateFromDocument(EntityStore entities, NoteDocument note) =>
        EditorItems.Create(entities, FromDocument(note), note.Item?.PrimaryShader ?? 0, note.Item?.PrimaryCommonShader ?? "");
}

/// <summary>Mines: timed hazards, picked like notes by their own size.</summary>
public sealed class MineItemKind : ItemKind<Mine, MineDocument>
{
    private const float HitRadius = PlayfieldRenderer.MineSize / 2;

    public override string Name => "mine";

    public override Vector2 Position(Entity entity) => Get(entity).Position;

    public override void SetPosition(Entity entity, Vector2 position) => Ref(entity).Position = position;

    public override long? Time(Entity entity) => Get(entity).TimeMs;

    public override bool SetTime(Entity entity, long timeMs)
    {
        Ref(entity).TimeMs = timeMs;
        return true;
    }

    public override long? TimeOf(object value) => ((Mine)value).TimeMs;

    public override bool Hit(Entity entity, Vector2 point, long playheadMs)
    {
        Mine mine = Get(entity);
        return MineFade.Alpha(mine, playheadMs) > 0 && TimedKinds.Within(point, mine.Position, HitRadius);
    }

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs)
    {
        Mine mine = Get(entity);
        if (MineFade.Alpha(mine, playheadMs) <= 0.05f || !EditorItemPicker.IsInside(mine.Position, playfield))
            return false;

        return screen.Intersects(TimedKinds.Square(entity, mine.Position, PlayfieldRenderer.MineSize, view));
    }

    public override PlayfieldDrawKind? DrawKind => PlayfieldDrawKind.Mine;

    public override void Draw(IRectCanvas canvas, Rid rid, Entity entity, PlayfieldView view, in PlayfieldItemFrame frame, bool selected, bool playing) =>
        PlayfieldRenderer.DrawMine(canvas, rid, entity, view, frame.Playhead);

    protected override int Compare(in Mine a, in Mine b) =>
        ByTimeThenPosition(a.TimeMs, a.Position, b.TimeMs, b.Position);

    protected override Mine? Pasted(Mine value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities) =>
        value with { TimeMs = timeMs };

    protected override List<MineDocument> List(IChartItems chart) => chart.Mines ??= new();

    protected override int CompareDocuments(MineDocument a, MineDocument b) => a.TimeMs.CompareTo(b.TimeMs);

    protected override MineDocument ToDocument(in Mine mine, Entity entity) => new()
    {
        TimeMs = mine.TimeMs,
        X = mine.Position.X,
        Y = mine.Position.Y,
        FadeInMs = mine.FadeInMs,
        FadeOutMs = mine.FadeOutMs,
    };

    protected override Mine FromDocument(MineDocument mine) =>
        new(mine.TimeMs, new Vector2(mine.X, mine.Y), mine.FadeInMs, mine.FadeOutMs);
}

/// <summary>Practice start points: timed, untinted, drawn only in the editor.</summary>
public sealed class StartPointItemKind : ItemKind<StartPoint, StartPointDocument>
{
    private const float HitRadius = PlayfieldRenderer.NoteSize / 2;

    public override string Name => "start point";

    public override bool Tintable => false;

    public override Vector2 Position(Entity entity) => Get(entity).Position;

    public override void SetPosition(Entity entity, Vector2 position) => Ref(entity).Position = position;

    public override long? Time(Entity entity) => Get(entity).TimeMs;

    public override bool SetTime(Entity entity, long timeMs)
    {
        Ref(entity).TimeMs = timeMs;
        return true;
    }

    public override long? TimeOf(object value) => ((StartPoint)value).TimeMs;

    /// <summary>Picked like a note, since it is drawn the size of one.</summary>
    public override bool Hit(Entity entity, Vector2 point, long playheadMs) =>
        Get(entity).Position.DistanceTo(point) <= HitRadius;

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs) =>
        screen.HasPoint(view.PlayfieldToScreen(GroupOffset.Shown(entity, Get(entity).Position)));

    public override PlayfieldDrawKind? DrawKind => PlayfieldDrawKind.StartPoint;

    public override void Draw(IRectCanvas canvas, Rid rid, Entity entity, PlayfieldView view, in PlayfieldItemFrame frame, bool selected, bool playing) =>
        PlayfieldRenderer.DrawStartPoint(canvas, rid, entity, view, selected);

    /// <summary>A start point keeps its groups on itself too, so a play launch can offset the spawn.</summary>
    protected override StartPoint Made(StartPoint value, in ItemHeader header) => value with { Groups = header.Groups };

    protected override int Compare(in StartPoint a, in StartPoint b) =>
        ByTimeThenPosition(a.TimeMs, a.Position, b.TimeMs, b.Position);

    protected override StartPoint? Pasted(StartPoint value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities) =>
        value with { TimeMs = timeMs };

    protected override List<StartPointDocument> List(IChartItems chart) => chart.StartPoints ??= new();

    protected override int CompareDocuments(StartPointDocument a, StartPointDocument b) => a.TimeMs.CompareTo(b.TimeMs);

    protected override StartPointDocument ToDocument(in StartPoint start, Entity entity) => new()
    {
        Name = start.Name,
        TimeMs = start.TimeMs,
        X = start.Position.X,
        Y = start.Position.Y,
        MomentumX = start.Momentum.X,
        MomentumY = start.Momentum.Y,
        DirectionX = start.Direction.X,
        DirectionY = start.Direction.Y,
    };

    protected override StartPoint FromDocument(StartPointDocument start) => new(
        start.Name,
        start.TimeMs,
        new Vector2(start.X, start.Y),
        new System.Numerics.Vector2(start.MomentumX, start.MomentumY),
        new Vector2(start.DirectionX, start.DirectionY));
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
        Vector2 center = view.PlayfieldToScreen(GroupOffset.Shown(entity, position));
        return new Rect2(center - new Vector2(side / 2, side / 2), new Vector2(side, side));
    }
}
