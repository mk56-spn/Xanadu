using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Editor;
using Xanadu.Items;
using Xanadu.Levels;
using Xanadu.Rendering;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;

namespace Xanadu.Ecs;

/// <summary>Text: a label turned freely about its centre.</summary>
public sealed class TextItemKind : ItemKind<LevelText, TextDocument>
{
    public override string Name => "text";

    public override int DefaultLayer => ItemLayer.Texts;

    public override Vector2 Position(Entity entity) => Get(entity).Position;

    public override void SetPosition(Entity entity, Vector2 position) => Ref(entity).Position = position;

    public override bool TurnsOnTheSpot => true;

    public override Rect2 Hull(Entity entity) => TurnedBox.Hull(Get(entity).Bounds, Get(entity).RotationDegrees);

    public override float Degrees(Entity entity) => Get(entity).RotationDegrees;

    public override Vector2 Size(Entity entity) => Get(entity).Bounds.Size;

    public override bool TryTurnFreely(Entity entity, Vector2 center, float degrees)
    {
        ref LevelText text = ref Ref(entity);
        text.Position = center;
        text.RotationDegrees = degrees;
        return true;
    }

    public override bool Hit(Entity entity, Vector2 point, long playheadMs)
    {
        LevelText text = Get(entity);
        return TurnedBox.Contains(text.Bounds, text.RotationDegrees, point);
    }

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs) =>
        screen.Intersects(PlayfieldRenderer.TextScreenHull(Get(entity), GroupOffset.View(entity, view)));

    public override PlayfieldDrawKind? DrawKind => PlayfieldDrawKind.Text;

    public override void Draw(IRectCanvas canvas, Rid rid, Entity entity, PlayfieldView view, in PlayfieldItemFrame frame, bool selected, bool playing) =>
        PlayfieldRenderer.DrawLevelText(canvas, rid, entity, view, selected);

    protected override int Compare(in LevelText a, in LevelText b)
    {
        int order = ByPosition(a.Position, b.Position);
        return order != 0 ? order : string.CompareOrdinal(a.Text, b.Text);
    }

    protected override LevelText? Pasted(LevelText value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities) =>
        value with { Position = value.Position + offset };

    protected override List<TextDocument> List(IChartItems chart) => chart.Texts ??= new();

    protected override int CompareDocuments(TextDocument a, TextDocument b) => ByXThenY(a.X, a.Y, b.X, b.Y);

    protected override TextDocument ToDocument(in LevelText text, Entity entity) => new()
    {
        X = text.Position.X,
        Y = text.Position.Y,
        Size = text.Size,
        Text = text.Text ?? "",
        Rotation = text.RotationDegrees,
    };

    protected override LevelText FromDocument(TextDocument text) =>
        new(new Vector2(text.X, text.Y), text.Text ?? "", text.Size, text.Rotation);
}

/// <summary>Grass: a box whose base sits on block tops, turned about the box's middle.</summary>
public sealed class GrassItemKind : ItemKind<GrassPatch, GrassDocument>
{
    public override string Name => "grass";

    public override int DefaultLayer => ItemLayer.Grass;

    /// <summary>The middle of the base line, where the blades stand.</summary>
    public override Vector2 Position(Entity entity) => Get(entity).Position;

    public override void SetPosition(Entity entity, Vector2 position) => Ref(entity).Position = position;

    public override bool TryGetBox(Entity entity, out Rect2 box, out float degrees)
    {
        GrassPatch grass = Get(entity);
        box = grass.Bounds;
        degrees = grass.RotationDegrees;
        return true;
    }

    public override void SetBox(Entity entity, Rect2 box) => Ref(entity).SetBounds(box);

    public override Vector2 SettleDrag(Vector2 corner, Vector2 size, Playfield playfield, bool snap) =>
        GridPlacement.SettleGrass(corner, size, playfield, snap);

    public override bool TurnsOnTheSpot => true;

    public override Rect2 Hull(Entity entity) => TurnedBox.Hull(Get(entity).Bounds, Get(entity).RotationDegrees);

    /// <summary>Grass turns about the middle of its box, not the foot of its blades.</summary>
    public override Vector2 TurnCenter(Entity entity) => Get(entity).Bounds.GetCenter();

    public override float Degrees(Entity entity) => Get(entity).RotationDegrees;

    public override Vector2 Size(Entity entity) => Get(entity).Bounds.Size;

    public override bool TryTurnFreely(Entity entity, Vector2 center, float degrees)
    {
        ref GrassPatch grass = ref Ref(entity);
        grass.Position = center + new Vector2(0, grass.Length / 2);
        grass.RotationDegrees = degrees;
        return true;
    }

    public override bool Hit(Entity entity, Vector2 point, long playheadMs)
    {
        GrassPatch grass = Get(entity);
        return TurnedBox.Contains(grass.Bounds, grass.RotationDegrees, point);
    }

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs) =>
        screen.Intersects(PlayfieldRenderer.GrassScreenBounds(Get(entity), GroupOffset.View(entity, view)));

    public override PlayfieldDrawKind? DrawKind => PlayfieldDrawKind.Grass;

    public override void Draw(IRectCanvas canvas, Rid rid, Entity entity, PlayfieldView view, in PlayfieldItemFrame frame, bool selected, bool playing) =>
        PlayfieldRenderer.DrawGrass(canvas, rid, entity, view, frame.Seconds, selected);

    protected override int Compare(in GrassPatch a, in GrassPatch b)
    {
        int order = ByPosition(a.Position, b.Position);
        return order != 0 ? order : a.Seed.CompareTo(b.Seed);
    }

    /// <summary>A fresh seed, so the copy is the same kind of grass but not the same blades.</summary>
    protected override GrassPatch? Pasted(GrassPatch value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities) =>
        value with { Position = value.Position + offset, Seed = GrassPatch.NewSeed() };

    protected override List<GrassDocument> List(IChartItems chart) => chart.Grass ??= new();

    protected override int CompareDocuments(GrassDocument a, GrassDocument b) => ByXThenY(a.X, a.Y, b.X, b.Y);

    protected override GrassDocument ToDocument(in GrassPatch patch, Entity entity) => new()
    {
        X = patch.Position.X,
        Y = patch.Position.Y,
        Width = patch.Width,
        Length = patch.Length,
        Density = patch.Density,
        Sway = patch.Sway,
        Color = patch.Color.ToRgba32(),
        Variation = patch.Variation,
        Seed = patch.Seed,
        Rotation = patch.RotationDegrees,
    };

    protected override GrassPatch FromDocument(GrassDocument grass) => LevelDocumentMapper.ToPatch(grass);
}

/// <summary>Flares: a box of shader light, turned about its centre, added onto what is under it.</summary>
public sealed class FlareItemKind : ItemKind<LevelFlare, FlareDocument>
{
    public override string Name => "flare";

    public override int DefaultLayer => ItemLayer.Flares;

    public override BlendMode DefaultBlend => BlendMode.Add;

    public override Vector2 Position(Entity entity) => Get(entity).Position;

    public override void SetPosition(Entity entity, Vector2 position) => Ref(entity).Position = position;

    public override bool TryGetBox(Entity entity, out Rect2 box, out float degrees)
    {
        LevelFlare flare = Get(entity);
        box = flare.Bounds;
        degrees = flare.RotationDegrees;
        return true;
    }

    public override void SetBox(Entity entity, Rect2 box) => Ref(entity).SetBounds(box);

    public override Vector2 SettleDrag(Vector2 corner, Vector2 size, Playfield playfield, bool snap) =>
        GridPlacement.SettleFlare(corner, size, playfield, snap);

    public override bool TurnsOnTheSpot => true;

    public override Rect2 Hull(Entity entity) => TurnedBox.Hull(Get(entity).Bounds, Get(entity).RotationDegrees);

    public override float Degrees(Entity entity) => Get(entity).RotationDegrees;

    public override Vector2 Size(Entity entity) => Get(entity).Bounds.Size;

    public override bool TryTurnFreely(Entity entity, Vector2 center, float degrees)
    {
        ref LevelFlare flare = ref Ref(entity);
        flare.Position = center;
        flare.RotationDegrees = degrees;
        return true;
    }

    public override bool Hit(Entity entity, Vector2 point, long playheadMs)
    {
        LevelFlare flare = Get(entity);
        return TurnedBox.Contains(flare.Bounds, flare.RotationDegrees, point);
    }

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs) =>
        screen.Intersects(PlayfieldRenderer.FlareScreenBounds(Get(entity), GroupOffset.View(entity, view)));

    public override PlayfieldDrawKind? DrawKind => PlayfieldDrawKind.Flare;

    public override void Draw(IRectCanvas canvas, Rid rid, Entity entity, PlayfieldView view, in PlayfieldItemFrame frame, bool selected, bool playing) =>
        PlayfieldRenderer.DrawFlare(canvas, rid, entity, view, selected);

    protected override int Compare(in LevelFlare a, in LevelFlare b)
    {
        int order = ByPosition(a.Position, b.Position);
        return order != 0 ? order : ByPosition(new Vector2(a.Width, a.Height), new Vector2(b.Width, b.Height));
    }

    protected override LevelFlare? Pasted(LevelFlare value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities) =>
        value with { Position = value.Position + offset };

    protected override List<FlareDocument> List(IChartItems chart) => chart.Flares ??= new();

    protected override int CompareDocuments(FlareDocument a, FlareDocument b) => ByXThenY(a.X, a.Y, b.X, b.Y);

    protected override FlareDocument ToDocument(in LevelFlare flare, Entity entity) => new()
    {
        X = flare.Position.X,
        Y = flare.Position.Y,
        Width = flare.Width,
        Height = flare.Height,
        Axes = flare.Axes,
        Thickness = flare.Thickness,
        Falloff = flare.Falloff,
        Core = flare.Core,
        Halo = flare.Halo,
        Glow = flare.Glow,
        Intensity = flare.Intensity,
        Heat = flare.Heat,
        Color = flare.Color.ToRgba32(),
        Rotation = flare.RotationDegrees,
    };

    protected override LevelFlare FromDocument(FlareDocument flare) => LevelDocumentMapper.ToFlare(flare);
}

/// <summary>Polygons: a Xif of shapes about an origin. The Xif is shared by reference, so every copy clones it.</summary>
public sealed class PolygonItemKind : ItemKind<LevelPolygon, PolygonDocument>
{
    public override string Name => "polygon";

    public override int DefaultLayer => ItemLayer.Polygons;

    public override Vector2 Position(Entity entity) => Get(entity).Position;

    public override void SetPosition(Entity entity, Vector2 position) => Ref(entity).Position = position;

    public override bool TurnsOnTheSpot => true;

    public override Rect2 Hull(Entity entity) => Get(entity).Bounds;

    public override float Degrees(Entity entity) => Get(entity).RotationDegrees;

    public override Vector2 Size(Entity entity) => Get(entity).LocalSize;

    public override bool TryTurnFreely(Entity entity, Vector2 center, float degrees)
    {
        ref LevelPolygon polygon = ref Ref(entity);
        polygon.Position = center;
        polygon.RotationDegrees = degrees;
        return true;
    }

    public override bool Hit(Entity entity, Vector2 point, long playheadMs) => Get(entity).Contains(point);

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs) =>
        screen.Intersects(PlayfieldRenderer.PolygonScreenBounds(Get(entity), GroupOffset.View(entity, view)));

    public override PlayfieldDrawKind? DrawKind => PlayfieldDrawKind.Polygon;

    public override void Draw(IRectCanvas canvas, Rid rid, Entity entity, PlayfieldView view, in PlayfieldItemFrame frame, bool selected, bool playing) =>
        PlayfieldRenderer.DrawPolygon(canvas, rid, entity, view, selected);

    protected override LevelPolygon Detach(LevelPolygon value) =>
        value with { Item = XifCopy.Clone(value.Item ?? new Xif()) };

    protected override bool Same(in LevelPolygon a, in LevelPolygon b) =>
        a.Position == b.Position &&
        a.RotationDegrees == b.RotationDegrees &&
        XifCopy.AreEqual(a.Item ?? new Xif(), b.Item ?? new Xif());

    protected override int Compare(in LevelPolygon a, in LevelPolygon b) => ByPosition(a.Position, b.Position);

    protected override LevelPolygon? Pasted(LevelPolygon value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities) =>
        value with { Position = value.Position + offset };

    protected override List<PolygonDocument> List(IChartItems chart) => chart.Polygons ??= new();

    protected override int CompareDocuments(PolygonDocument a, PolygonDocument b) => ByXThenY(a.X, a.Y, b.X, b.Y);

    protected override PolygonDocument ToDocument(in LevelPolygon polygon, Entity entity) => new()
    {
        X = polygon.Position.X,
        Y = polygon.Position.Y,
        Rotation = polygon.RotationDegrees,
        Item = XifCopy.Clone(polygon.Item ?? new Xif()),
    };

    protected override LevelPolygon FromDocument(PolygonDocument polygon) =>
        new(new Vector2(polygon.X, polygon.Y), XifCopy.Clone(polygon.Item ?? new Xif()), polygon.Rotation);
}

/// <summary>Screen-read regions: authoring rects with no body; the editor outlines them.</summary>
public sealed class ScreenReadItemKind : ItemKind<ScreenReadRegion, ScreenReadDocument>
{
    public override string Name => "screen-read region";

    public override bool Tintable => false;

    public override Vector2 Position(Entity entity) => Get(entity).Bounds.GetCenter();

    public override void SetPosition(Entity entity, Vector2 position)
    {
        ref ScreenReadRegion region = ref Ref(entity);
        region.Position = position - region.Size / 2;
    }

    public override bool TryGetBox(Entity entity, out Rect2 box, out float degrees)
    {
        box = Get(entity).Bounds;
        degrees = 0f;
        return true;
    }

    public override void SetBox(Entity entity, Rect2 box)
    {
        ref ScreenReadRegion region = ref Ref(entity);
        region.Position = box.Position;
        region.Size = box.Size;
    }

    public override bool Hit(Entity entity, Vector2 point, long playheadMs) => Get(entity).Contains(point);

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs)
    {
        ScreenReadRegion region = Get(entity);
        return screen.Intersects(new Rect2(view.PlayfieldToScreen(GroupOffset.Shown(entity, region.Position)), region.Size * view.Scale));
    }

    public override PlayfieldDrawKind? DrawKind => PlayfieldDrawKind.ScreenRead;

    public override void Draw(IRectCanvas canvas, Rid rid, Entity entity, PlayfieldView view, in PlayfieldItemFrame frame, bool selected, bool playing) =>
        PlayfieldRenderer.DrawScreenRead(canvas, entity, view);

    protected override int Compare(in ScreenReadRegion a, in ScreenReadRegion b)
    {
        int order = ByPosition(a.Position, b.Position);
        return order != 0 ? order : ByPosition(a.Size, b.Size);
    }

    protected override ScreenReadRegion? Pasted(ScreenReadRegion value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities) =>
        value with { Position = GridPlacement.Clamp(value.Position + offset, value.Size, playfield) };

    protected override List<ScreenReadDocument> List(IChartItems chart) => chart.ScreenReads ??= new();

    protected override int CompareDocuments(ScreenReadDocument a, ScreenReadDocument b) => ByXThenY(a.X, a.Y, b.X, b.Y);

    protected override ScreenReadDocument ToDocument(in ScreenReadRegion region, Entity entity) => new()
    {
        X = region.Position.X,
        Y = region.Position.Y,
        Width = region.Size.X,
        Height = region.Size.Y,
    };

    protected override ScreenReadRegion FromDocument(ScreenReadDocument region) =>
        new(new Vector2(region.X, region.Y), new Vector2(region.Width, region.Height));
}
