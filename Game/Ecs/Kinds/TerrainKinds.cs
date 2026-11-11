using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Editor;
using Xanadu.Levels;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;

namespace Xanadu.Ecs;

/// <summary>Blocks: grid boxes the player collides with, turned only in quarter turns, which swap their size.</summary>
public sealed class BlockItemKind : ItemKind<Block, BlockDocument>
{
    public override string Name => "block";

    public override int DefaultLayer => ItemLayer.Terrain;

    public override Vector2 Position(Entity entity) => Get(entity).Center;

    public override void SetPosition(Entity entity, Vector2 position)
    {
        ref Block block = ref Ref(entity);
        block.Position = position - block.Size / 2;
    }

    public override bool TryGetBox(Entity entity, out Rect2 box, out float degrees)
    {
        box = Get(entity).Bounds;
        degrees = 0f;
        return true;
    }

    public override void SetBox(Entity entity, Rect2 box)
    {
        ref Block block = ref Ref(entity);
        block.Position = box.Position;
        block.Size = box.Size;
    }

    public override bool TurnsOnTheSpot => true;

    public override Rect2 Hull(Entity entity) => Get(entity).Bounds;

    public override Vector2 Size(Entity entity) => Get(entity).Size;

    public override bool Hit(Entity entity, Vector2 point, long playheadMs) => Get(entity).Bounds.HasPoint(point);

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs)
    {
        Block block = Get(entity);
        return screen.Intersects(new Rect2(view.PlayfieldToScreen(GroupOffset.Shown(entity, block.Position)), block.Size * view.Scale));
    }

    public override PlayfieldDrawKind? DrawKind => PlayfieldDrawKind.Block;

    public override void Draw(IRectCanvas canvas, Handle rid, Entity entity, PlayfieldView view, in PlayfieldItemFrame frame, bool selected, bool playing) =>
        PlayfieldRenderer.DrawBlock(canvas, rid, entity, view, selected, playing);

    protected override int Compare(in Block a, in Block b)
    {
        int order = ByPosition(a.Position, b.Position);
        return order != 0 ? order : ByPosition(a.Size, b.Size);
    }

    protected override Block? Pasted(Block value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities)
    {
        value.Position = GridPlacement.Clamp(value.Position + offset, value.Size, playfield);
        return PlacementCollision.BlockFits(entities, value.Bounds) ? value : null;
    }

    protected override List<BlockDocument> List(IChartItems chart) => chart.Blocks ??= new();

    protected override int CompareDocuments(BlockDocument a, BlockDocument b) => ByXThenY(a.X, a.Y, b.X, b.Y);

    protected override BlockDocument ToDocument(in Block block, Entity entity) => new()
    {
        X = block.Position.X,
        Y = block.Position.Y,
        Width = block.Size.X,
        Height = block.Size.Y,
        OneWay = (int)block.OneWay,
        OutlineWidth = block.OutlineWidth,
        OutlineColor = block.OutlineColor.ToRgba32(),
        FillGroups = block.FillGroups,
        OutlineGroups = block.OutlineGroups,
    };

    protected override Block FromDocument(BlockDocument block) => new(
        new Vector2(block.X, block.Y),
        new Vector2(block.Width, block.Height),
        (BlockOneWay)block.OneWay,
        block.OutlineWidth,
        new Color(block.OutlineColor),
        block.FillGroups,
        block.OutlineGroups);
}

/// <summary>Spikes: grid boxes that kill on contact, their tip turned freely within the box.</summary>
public sealed class SpikeItemKind : ItemKind<Spike, SpikeDocument>
{
    public override string Name => "spike";

    public override int DefaultLayer => ItemLayer.Terrain;

    public override Vector2 Position(Entity entity) => Get(entity).Center;

    public override void SetPosition(Entity entity, Vector2 position)
    {
        ref Spike spike = ref Ref(entity);
        spike.Position = position - spike.Size / 2;
    }

    public override bool TryGetBox(Entity entity, out Rect2 box, out float degrees)
    {
        Spike spike = Get(entity);
        box = spike.Bounds;
        degrees = spike.RotationDegrees;
        return true;
    }

    public override void SetBox(Entity entity, Rect2 box)
    {
        ref Spike spike = ref Ref(entity);
        spike.Position = box.Position;
        spike.Size = box.Size;
    }

    public override bool TurnsOnTheSpot => true;

    public override Rect2 Hull(Entity entity) => TurnedBox.Hull(Get(entity).Bounds, Get(entity).RotationDegrees);

    public override float Degrees(Entity entity) => Get(entity).RotationDegrees;

    public override Vector2 Size(Entity entity) => Get(entity).Size;

    public override bool Hit(Entity entity, Vector2 point, long playheadMs) => Get(entity).Contains(point);

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs)
    {
        Spike spike = Get(entity);
        return screen.Intersects(new Rect2(view.PlayfieldToScreen(GroupOffset.Shown(entity, spike.Position)), spike.Size * view.Scale));
    }

    public override PlayfieldDrawKind? DrawKind => PlayfieldDrawKind.Spike;

    public override void Draw(IRectCanvas canvas, Handle rid, Entity entity, PlayfieldView view, in PlayfieldItemFrame frame, bool selected, bool playing) =>
        PlayfieldRenderer.DrawSpike(canvas, rid, entity, view, selected);

    protected override int Compare(in Spike a, in Spike b)
    {
        int order = ByPosition(a.Position, b.Position);
        return order != 0 ? order : a.RotationDegrees.CompareTo(b.RotationDegrees);
    }

    protected override Spike? Pasted(Spike value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities)
    {
        value.Position = GridPlacement.Clamp(value.Position + offset, value.Size, playfield);
        return PlacementCollision.SpikeFits(entities, value.Bounds) ? value : null;
    }

    protected override List<SpikeDocument> List(IChartItems chart) => chart.Spikes ??= new();

    protected override int CompareDocuments(SpikeDocument a, SpikeDocument b) => ByXThenY(a.X, a.Y, b.X, b.Y);

    protected override SpikeDocument ToDocument(in Spike spike, Entity entity) => new()
    {
        X = spike.Position.X,
        Y = spike.Position.Y,
        Width = spike.Size.X,
        Height = spike.Size.Y,
        RotationDegrees = spike.RotationDegrees,
        OutlineWidth = spike.OutlineWidth,
        OutlineColor = spike.OutlineColor.ToRgba32(),
    };

    protected override Spike FromDocument(SpikeDocument spike) => new(
        new Vector2(spike.X, spike.Y),
        new Vector2(spike.Width, spike.Height),
        spike.RotationDegrees,
        spike.OutlineWidth,
        new Color(spike.OutlineColor));
}

/// <summary>Friction bars: upright lines that turn ground friction on when the player crosses one. Only their height changes.</summary>
public sealed class FrictionBarItemKind : ItemKind<FrictionBar, FrictionBarDocument>
{
    public override string Name => "friction bar";

    public override int DefaultLayer => ItemLayer.Terrain;

    public override Vector2 Position(Entity entity) => Get(entity).Center;

    public override void SetPosition(Entity entity, Vector2 position)
    {
        ref FrictionBar bar = ref Ref(entity);
        bar.Position = position - new Vector2(FrictionBar.Thickness, bar.Height) / 2;
    }

    public override bool TryGetBox(Entity entity, out Rect2 box, out float degrees)
    {
        box = Get(entity).Bounds;
        degrees = 0f;
        return true;
    }

    /// <summary>A resize changes only the height; the bar keeps its line and its thickness.</summary>
    public override void SetBox(Entity entity, Rect2 box)
    {
        ref FrictionBar bar = ref Ref(entity);
        bar.Position = new Vector2(box.GetCenter().X - FrictionBar.Thickness / 2, box.Position.Y);
        bar.Height = box.Size.Y;
    }

    public override Vector2 SettleDrag(Vector2 corner, Vector2 size, Playfield playfield, bool snap) =>
        GridPlacement.SettleFrictionBar(corner, size, playfield, snap);

    public override Rect2 Hull(Entity entity) => Get(entity).Bounds;

    public override Vector2 Size(Entity entity) => Get(entity).Bounds.Size;

    public override bool Hit(Entity entity, Vector2 point, long playheadMs) =>
        Get(entity).Bounds.Grow(FrictionBar.Thickness).HasPoint(point);

    public override bool InMarquee(Entity entity, Rect2 screen, PlayfieldView view, Playfield playfield, long playheadMs) =>
        screen.Intersects(PlayfieldRenderer.FrictionBarScreenBounds(Get(entity), GroupOffset.View(entity, view)));

    public override PlayfieldDrawKind? DrawKind => PlayfieldDrawKind.FrictionBar;

    public override void Draw(IRectCanvas canvas, Handle rid, Entity entity, PlayfieldView view, in PlayfieldItemFrame frame, bool selected, bool playing) =>
        PlayfieldRenderer.DrawFrictionBar(canvas, rid, entity, view, selected);

    protected override int Compare(in FrictionBar a, in FrictionBar b)
    {
        int order = ByPosition(a.Position, b.Position);
        return order != 0 ? order : a.Height.CompareTo(b.Height);
    }

    protected override FrictionBar? Pasted(FrictionBar value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities)
    {
        value.Position = GridPlacement.Clamp(value.Position + offset, value.Bounds.Size, playfield);
        return value;
    }

    protected override List<FrictionBarDocument> List(IChartItems chart) => chart.FrictionBars ??= new();

    protected override int CompareDocuments(FrictionBarDocument a, FrictionBarDocument b) => ByXThenY(a.X, a.Y, b.X, b.Y);

    protected override FrictionBarDocument ToDocument(in FrictionBar bar, Entity entity) => new()
    {
        X = bar.Position.X,
        Y = bar.Position.Y,
        Height = bar.Height,
    };

    protected override FrictionBar FromDocument(FrictionBarDocument bar) => new(new Vector2(bar.X, bar.Y), bar.Height);
}
