using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Editor;

/// <summary>Rebuilds the level's editable items from a snapshot when undo or redo lands.</summary>
public sealed class EditorSnapshotRestore(EntityStore entities)
{
    public void Apply(EditorSnapshot snapshot)
    {
        foreach (Entity entity in entities.Query<Note>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in entities.Query<Mine>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in entities.Query<StartPoint>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in entities.Query<Block>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in entities.Query<Spike>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in entities.Query<LevelText>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in entities.Query<GrassPatch>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in entities.Query<LevelPolygon>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in entities.Query<ScreenReadRegion>().ToEntityList())
            entity.DeleteEntity();

        if (snapshot.Groups is not null)
        {
            foreach (Entity entity in entities.Query<LevelGroups>().ToEntityList())
            {
                entity.GetComponent<LevelGroups>() = new LevelGroups { Groups = snapshot.Groups }.Clone();
                break;
            }
        }

        if (snapshot.Layers is { } layers)
        {
            foreach (Entity entity in entities.Query<LevelLayers>().ToEntityList())
            {
                entity.GetComponent<LevelLayers>() = layers.Clone();
                break;
            }
        }

        foreach (NoteSnapshot note in snapshot.Notes)
        {
            Entity created = EditorItems.Create(entities, new Note(
                note.TimeMs,
                note.Position,
                note.Target,
                note.Kind,
                note.DashDirection,
                note.ContactRequired,
                note.Character,
                note.DashStrength,
                note.PairIndex,
                note.Momentum,
                note.ContactLeft,
                note.ContactRight,
                note.ChannelSide,
                note.HoldMs,
                note.ShapeId,
                note.ShapePiece), note.Shader);
            Subscribe(created, note.Groups);
            ItemLayer.Put(created, note.Layer);
            ItemBlend.Put(created, note.Blend);
            ItemShaderSource.Put(created, note.ShaderSource);
            SelfModulate.Put(created, note.Modulate);
        }

        foreach (MineSnapshot mine in snapshot.Mines)
        {
            Entity created = EditorItems.Create(entities, new Mine(
                mine.TimeMs,
                mine.Position,
                mine.FadeInMs,
                mine.FadeOutMs), mine.Shader);
            Subscribe(created, mine.Groups);
            ItemLayer.Put(created, mine.Layer);
            ItemBlend.Put(created, mine.Blend);
            ItemShaderSource.Put(created, mine.ShaderSource);
            SelfModulate.Put(created, mine.Modulate);
        }

        foreach (StartPointSnapshot start in snapshot.StartPoints)
        {
            Entity created = EditorItems.Create(entities, new StartPoint(
                start.Name,
                start.TimeMs,
                start.Position,
                start.Momentum,
                start.Direction,
                start.Groups));
            Subscribe(created, start.Groups);
            ItemLayer.Put(created, start.Layer);
            ItemBlend.Put(created, start.Blend);
            ItemShaderSource.Put(created, start.ShaderSource);
        }

        foreach (BlockSnapshot block in snapshot.Blocks)
        {
            Entity created = EditorItems.Create(entities, new Block(
                block.Position,
                block.Size,
                block.ShapeId,
                block.ShapePiece,
                block.OneWay,
                block.OutlineWidth,
                new Color(block.OutlineColor),
                block.FillGroups,
                block.OutlineGroups), block.Shader);
            if (block.ShapeId > 0)
                created.AddTag<ConstructionHidden>();
            Subscribe(created, block.Groups);
            ItemLayer.Put(created, block.Layer);
            ItemBlend.Put(created, block.Blend);
            ItemShaderSource.Put(created, block.ShaderSource);
            SelfModulate.Put(created, block.Modulate);
        }

        foreach (SpikeSnapshot spike in snapshot.Spikes)
        {
            Entity created = EditorItems.Create(entities, new Spike(
                    spike.Position,
                    spike.Size,
                    spike.RotationDegrees,
                    spike.OutlineWidth,
                    new Color(spike.OutlineColor)),
                spike.Shader);
            Subscribe(created, spike.Groups);
            ItemLayer.Put(created, spike.Layer);
            ItemBlend.Put(created, spike.Blend);
            ItemShaderSource.Put(created, spike.ShaderSource);
            SelfModulate.Put(created, spike.Modulate);
        }

        foreach (TextSnapshot text in snapshot.Texts ?? new())
        {
            Entity created = EditorItems.Create(
                entities,
                new LevelText(text.Position, text.Text, text.Size, text.Rotation),
                text.Shader);
            Subscribe(created, text.Groups);
            ItemLayer.Put(created, text.Layer);
            ItemBlend.Put(created, text.Blend);
            ItemShaderSource.Put(created, text.ShaderSource);
            SelfModulate.Put(created, text.Modulate);
        }

        foreach (GrassSnapshot patch in snapshot.Grass ?? new())
        {
            Entity created = EditorItems.Create(entities, patch.Patch, patch.Shader);
            Subscribe(created, patch.Groups);
            ItemLayer.Put(created, patch.Layer);
            ItemBlend.Put(created, patch.Blend);
            ItemShaderSource.Put(created, patch.ShaderSource);
            SelfModulate.Put(created, patch.Modulate);
        }

        // Each undo hands out its own copy, so later edits never reach the history.
        foreach (PolygonSnapshot polygon in snapshot.Polygons ?? new())
        {
            Entity created = EditorItems.Create(entities, new LevelPolygon(
                polygon.Position,
                Xanadu.Items.XifCopy.Clone(polygon.Item),
                polygon.Rotation), polygon.Shader);
            Subscribe(created, polygon.Groups);
            ItemLayer.Put(created, polygon.Layer);
            ItemBlend.Put(created, polygon.Blend);
            ItemShaderSource.Put(created, polygon.ShaderSource);
            SelfModulate.Put(created, polygon.Modulate);
        }

        foreach (ScreenReadSnapshot region in snapshot.ScreenReads ?? new())
        {
            Entity created = EditorItems.Create(
                entities,
                new ScreenReadRegion(region.Position, region.Size));
            ItemLayer.Put(created, region.Layer);
        }
    }

    private static void Subscribe(Entity item, uint mask)
    {
        if (mask != 0)
            item.AddComponent(new ItemGroups(mask));
    }
}
