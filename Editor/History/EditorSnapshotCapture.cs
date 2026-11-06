using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;

namespace Xanadu.Editor;

/// <summary>Reads the level's editable items into a snapshot the undo history can compare.</summary>
public sealed class EditorSnapshotCapture(EntityStore entities)
{
    public EditorSnapshot Capture()
    {
        var notes = new List<NoteSnapshot>();
        var mines = new List<MineSnapshot>();
        var starts = new List<StartPointSnapshot>();
        var blocks = new List<BlockSnapshot>();
        var spikes = new List<SpikeSnapshot>();
        var texts = new List<TextSnapshot>();
        var grass = new List<GrassSnapshot>();
        var polygons = new List<PolygonSnapshot>();
        var screenReads = new List<ScreenReadSnapshot>();

        foreach (Entity entity in entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            notes.Add(new NoteSnapshot(
                note.TimeMs,
                note.Position,
                note.Target,
                note.Kind,
                note.DashDirection,
                note.ContactRequired,
                note.Character,
                MaskOf(entity),
                ItemShader.Of(entity),
                note.DashStrength,
                note.PairIndex,
                note.Momentum,
                note.ContactLeft,
                note.ContactRight,
                note.ChannelSide,
                note.HoldMs,
                note.ShapeId,
                note.ShapePiece,
                SelfModulate.Packed(entity),
                ItemLayer.Of(entity),
                ItemBlend.Of(entity),
                ItemShaderSource.Of(entity)));
        }

        notes.Sort(static (a, b) =>
        {
            int order = SnapshotOrder.ByTimeThenPosition(a.TimeMs, a.Position, b.TimeMs, b.Position);
            if (order != 0)
                return order;

            int strength = a.DashStrength.CompareTo(b.DashStrength);
            if (strength != 0)
                return strength;

            int pair = a.PairIndex.CompareTo(b.PairIndex);
            if (pair != 0)
                return pair;

            int momentum = a.Momentum.CompareTo(b.Momentum);
            if (momentum != 0)
                return momentum;

            int contact = a.ContactLeft.CompareTo(b.ContactLeft);
            if (contact != 0)
                return contact;

            int right = a.ContactRight.CompareTo(b.ContactRight);
            if (right != 0)
                return right;

            int side = a.ChannelSide.CompareTo(b.ChannelSide);
            return side != 0 ? side : a.HoldMs.CompareTo(b.HoldMs);
        });

        foreach (Entity entity in entities.Query<Mine>().ToEntityList())
        {
            Mine mine = entity.GetComponent<Mine>();
            mines.Add(new MineSnapshot(
                mine.TimeMs,
                mine.Position,
                mine.FadeInMs,
                mine.FadeOutMs,
                MaskOf(entity),
                ItemShader.Of(entity),
                SelfModulate.Packed(entity),
                ItemLayer.Of(entity),
                ItemBlend.Of(entity),
                ItemShaderSource.Of(entity)));
        }

        mines.Sort(static (a, b) =>
            SnapshotOrder.ByTimeThenPosition(a.TimeMs, a.Position, b.TimeMs, b.Position));

        foreach (Entity entity in entities.Query<StartPoint>().ToEntityList())
        {
            StartPoint start = entity.GetComponent<StartPoint>();
            starts.Add(new StartPointSnapshot(
                start.Name,
                start.TimeMs,
                start.Position,
                start.Momentum,
                start.Direction,
                MaskOf(entity),
                ItemLayer.Of(entity),
                ItemBlend.Of(entity),
                ItemShaderSource.Of(entity)));
        }

        starts.Sort(static (a, b) =>
            SnapshotOrder.ByTimeThenPosition(a.TimeMs, a.Position, b.TimeMs, b.Position));

        foreach (Entity entity in entities.Query<Block>().ToEntityList())
        {
            Block block = entity.GetComponent<Block>();
            blocks.Add(new BlockSnapshot(
                block.Position,
                block.Size,
                MaskOf(entity),
                ItemShader.Of(entity),
                block.ShapeId,
                block.ShapePiece,
                SelfModulate.Packed(entity),
                block.OneWay,
                block.OutlineWidth,
                block.OutlineColor.ToRgba32(),
                block.FillGroups,
                block.OutlineGroups,
                ItemLayer.Of(entity),
                ItemBlend.Of(entity),
                ItemShaderSource.Of(entity)));
        }

        blocks.Sort(static (a, b) => SnapshotOrder.ByPosition(a.Position, b.Position));

        foreach (Entity entity in entities.Query<Spike>().ToEntityList())
        {
            Spike spike = entity.GetComponent<Spike>();
            spikes.Add(new SpikeSnapshot(
                spike.Position,
                spike.Size,
                MaskOf(entity),
                ItemShader.Of(entity),
                SelfModulate.Packed(entity),
                spike.RotationDegrees,
                spike.OutlineWidth,
                spike.OutlineColor.ToRgba32(),
                ItemLayer.Of(entity),
                ItemBlend.Of(entity),
                ItemShaderSource.Of(entity)));
        }

        spikes.Sort(static (a, b) => SnapshotOrder.ByPosition(a.Position, b.Position));

        foreach (Entity entity in entities.Query<LevelText>().ToEntityList())
        {
            LevelText text = entity.GetComponent<LevelText>();
            texts.Add(new TextSnapshot(
                text.Position,
                text.Text ?? "",
                text.Size,
                MaskOf(entity),
                ItemShader.Of(entity),
                SelfModulate.Packed(entity),
                text.RotationDegrees,
                ItemLayer.Of(entity),
                ItemBlend.Of(entity),
                ItemShaderSource.Of(entity)));
        }

        texts.Sort(static (a, b) =>
        {
            int order = SnapshotOrder.ByPosition(a.Position, b.Position);
            return order != 0 ? order : string.CompareOrdinal(a.Text, b.Text);
        });

        foreach (Entity entity in entities.Query<GrassPatch>().ToEntityList())
        {
            grass.Add(new GrassSnapshot(
                entity.GetComponent<GrassPatch>(),
                MaskOf(entity),
                ItemShader.Of(entity),
                SelfModulate.Packed(entity),
                ItemLayer.Of(entity),
                ItemBlend.Of(entity),
                ItemShaderSource.Of(entity)));
        }

        grass.Sort(static (a, b) =>
        {
            int order = SnapshotOrder.ByPosition(a.Patch.Position, b.Patch.Position);
            return order != 0 ? order : a.Patch.Seed.CompareTo(b.Patch.Seed);
        });

        foreach (Entity entity in entities.Query<LevelPolygon>().ToEntityList())
        {
            LevelPolygon polygon = entity.GetComponent<LevelPolygon>();
            polygons.Add(new PolygonSnapshot(
                polygon.Position,
                polygon.RotationDegrees,
                Xanadu.Items.XifCopy.Clone(polygon.Item ?? new Xanadu.Items.Xif()),
                MaskOf(entity),
                ItemShader.Of(entity),
                SelfModulate.Packed(entity),
                ItemLayer.Of(entity),
                ItemBlend.Of(entity),
                ItemShaderSource.Of(entity)));
        }

        polygons.Sort(static (a, b) => SnapshotOrder.ByPosition(a.Position, b.Position));

        foreach (Entity entity in entities.Query<ScreenReadRegion>().ToEntityList())
        {
            ScreenReadRegion region = entity.GetComponent<ScreenReadRegion>();
            screenReads.Add(new ScreenReadSnapshot(
                region.Position,
                region.Size,
                ItemLayer.Of(entity),
                entity.TryGetComponent(out ItemGroups regionGroups) ? regionGroups.Mask : 0u));
        }

        screenReads.Sort(static (a, b) => SnapshotOrder.ByPosition(a.Position, b.Position));

        return new EditorSnapshot(
            notes,
            mines,
            starts,
            blocks,
            spikes,
            CaptureGroups(),
            texts,
            grass,
            polygons,
            CaptureLayers(),
            screenReads);
    }

    private static uint MaskOf(Entity entity) =>
        entity.TryGetComponent(out ItemGroups groups) ? groups.Mask : 0u;

    private LevelLayers? CaptureLayers()
    {
        foreach (Entity entity in entities.Query<LevelLayers>().ToEntityList())
            return entity.GetComponent<LevelLayers>().Clone();
        return null;
    }

    private GroupDefinition[]? CaptureGroups()
    {
        foreach (Entity entity in entities.Query<LevelGroups>().ToEntityList())
            return entity.GetComponent<LevelGroups>().Clone().Groups;
        return null;
    }
}
