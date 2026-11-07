using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Picks the playfield item under a point and the items inside a box, in the
/// order the editor treats them: notes, mines, start points, texts, grass,
/// spikes, blocks, polygons. Spikes and text sit over blocks; polygons sit
/// under everything. Items are found where they're shown at the playhead, with
/// their groups' moves applied. Items on hidden or locked layers can't be picked.
/// </summary>
public sealed class EditorItemPicker(EditorSceneQuery scene)
{
    private const float NoteHitRadius = PlayfieldRenderer.NoteSize / 2;
    private const float MineHitRadius = PlayfieldRenderer.MineSize / 2;

    private readonly EntityStore _entities = scene.Store;

    /// <summary>
    /// The topmost item a press at this point would pick, or null. A selected
    /// note or mine comes first and is found even when faded out at the
    /// playhead, since its selection ring still shows: a press inside the ring
    /// always grabs it.
    /// </summary>
    public Entity FindItemAt(Vector2 position, long playheadMs)
    {
        Entity hit = FindSelectedMarkAt(position);
        if (hit.IsNull)
            hit = FindNoteAt(position, playheadMs);
        if (hit.IsNull)
            hit = FindMineAt(position, playheadMs);
        if (hit.IsNull)
            hit = FindStartAt(position);
        if (hit.IsNull)
            hit = FindTextAt(position);
        if (hit.IsNull)
            hit = FindGrassAt(position);
        if (hit.IsNull)
            hit = FindSpikeAt(position);
        if (hit.IsNull)
            hit = FindBlockAt(position);
        if (hit.IsNull)
            hit = FindPolygonAt(position);
        if (hit.IsNull)
            hit = FindRegionAt(position);
        return hit;
    }

    public Entity FindBlockAt(Vector2 position)
    {
        EditorLayers layers = Layers();
        Entity result = default;
        foreach (Entity entity in _entities.Query<Block>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            if (entity.GetComponent<Block>().Bounds.HasPoint(Placed(entity, position)))
                result = entity;
        }

        return result;
    }

    public Entity FindTextAt(Vector2 position)
    {
        EditorLayers layers = Layers();
        Entity result = default;
        foreach (Entity entity in _entities.Query<LevelText>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            LevelText text = entity.GetComponent<LevelText>();
            if (TurnedBox.Contains(text.Bounds, text.RotationDegrees, Placed(entity, position)))
                result = entity;
        }

        return result;
    }

    public Entity FindGrassAt(Vector2 position)
    {
        EditorLayers layers = Layers();
        Entity result = default;
        foreach (Entity entity in _entities.Query<GrassPatch>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            GrassPatch grass = entity.GetComponent<GrassPatch>();
            if (TurnedBox.Contains(grass.Bounds, grass.RotationDegrees, Placed(entity, position)))
                result = entity;
        }

        return result;
    }

    public Entity FindPolygonAt(Vector2 position)
    {
        EditorLayers layers = Layers();
        Entity result = default;
        foreach (Entity entity in _entities.Query<LevelPolygon>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            if (entity.GetComponent<LevelPolygon>().Contains(Placed(entity, position)))
                result = entity;
        }

        return result;
    }

    /// <summary>
    /// The screen-read region under a point. Regions sit under every other item
    /// when picking, since they are large rects meant to enclose things.
    /// </summary>
    public Entity FindRegionAt(Vector2 position)
    {
        EditorLayers layers = Layers();
        Entity result = default;
        foreach (Entity entity in _entities.Query<ScreenReadRegion>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            if (entity.GetComponent<ScreenReadRegion>().Contains(Placed(entity, position)))
                result = entity;
        }

        return result;
    }

    public Entity FindSpikeAt(Vector2 position)
    {
        EditorLayers layers = Layers();
        Entity result = default;
        foreach (Entity entity in _entities.Query<Spike>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            if (entity.GetComponent<Spike>().Contains(Placed(entity, position)))
                result = entity;
        }

        return result;
    }

    public Entity FindNoteAt(Vector2 position, long playheadMs)
    {
        EditorLayers layers = Layers();
        Entity result = default;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            Note note = entity.GetComponent<Note>();
            if (NoteFade.Alpha(note, playheadMs) <= 0)
                continue;

            if (Within(Placed(entity, position), note.Position, NoteHitRadius))
            {
                result = entity;
            }
        }

        return result;
    }

    /// <summary>The selected note or mine under a point, whatever its fade, or null.</summary>
    public Entity FindSelectedMarkAt(Vector2 position)
    {
        Entity editor = scene.Editor();
        if (editor.IsNull || !editor.HasComponent<NoteSelection>())
            return default;

        EditorLayers layers = Layers();
        Entity result = default;
        foreach (Entity entity in editor.GetComponent<NoteSelection>().AllNotes)
        {
            if (!EditorItems.IsItem(entity) || !layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            if (entity.TryGetComponent(out Note note) &&
                Within(Placed(entity, position), note.Position, NoteHitRadius))
            {
                result = entity;
            }
            else if (entity.TryGetComponent(out Mine mine) &&
                     Within(Placed(entity, position), mine.Position, MineHitRadius))
            {
                result = entity;
            }
        }

        return result;
    }

    public Entity FindMineAt(Vector2 position, long playheadMs)
    {
        EditorLayers layers = Layers();
        Entity result = default;

        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            Mine mine = entity.GetComponent<Mine>();
            if (MineFade.Alpha(mine, playheadMs) <= 0)
                continue;

            if (Within(Placed(entity, position), mine.Position, MineHitRadius))
            {
                result = entity;
            }
        }

        return result;
    }

    public Entity FindStartAt(Vector2 position)
    {
        EditorLayers layers = Layers();
        Entity result = default;
        foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            StartPoint startPoint = entity.GetComponent<StartPoint>();
            // Picked like a note, since it is drawn the size of one.
            if (startPoint.Position.DistanceTo(Placed(entity, position)) <= NoteHitRadius)
                result = entity;
        }

        return result;
    }

    /// <summary>Every playfield item whose drawn bounds intersect a screen box.</summary>
    public List<Entity> FindNotesIn(Rect2 screenBounds, long playheadMs)
    {
        var selected = new List<Entity>();
        Entity editor = scene.Editor();
        if (editor.IsNull)
            return selected;

        EditorLayers layers = EditorLayers.Of(editor);

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        float noteSize = PlayfieldRenderer.NoteSize * view.Scale;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            Note note = entity.GetComponent<Note>();
            if (NoteRules.IsRail(note.Kind) ||
                NoteRules.IsChannelEnd(note.Kind) ||
                NoteFade.Alpha(note, playheadMs) <= 0.05f ||
                !IsInside(note.Position, playfield))
            {
                continue;
            }

            Vector2 center = view.PlayfieldToScreen(GroupOffset.Shown(entity, note.Position));
            var noteBounds = new Rect2(
                center - new Vector2(noteSize / 2, noteSize / 2),
                new Vector2(noteSize, noteSize));
            if (screenBounds.Intersects(noteBounds))
                selected.Add(entity);
        }

        float mineSize = PlayfieldRenderer.MineSize * view.Scale;
        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            Mine mine = entity.GetComponent<Mine>();
            if (MineFade.Alpha(mine, playheadMs) <= 0.05f ||
                !IsInside(mine.Position, playfield))
            {
                continue;
            }

            Vector2 center = view.PlayfieldToScreen(GroupOffset.Shown(entity, mine.Position));
            var mineBounds = new Rect2(
                center - new Vector2(mineSize / 2, mineSize / 2),
                new Vector2(mineSize, mineSize));
            if (screenBounds.Intersects(mineBounds))
                selected.Add(entity);
        }

        foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            StartPoint start = entity.GetComponent<StartPoint>();
            Vector2 center = view.PlayfieldToScreen(GroupOffset.Shown(entity, start.Position));
            if (screenBounds.HasPoint(center))
                selected.Add(entity);
        }

        foreach (Entity entity in _entities.Query<LevelText>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            if (screenBounds.Intersects(PlayfieldRenderer.TextScreenHull(entity.GetComponent<LevelText>(), GroupOffset.View(entity, view))))
                selected.Add(entity);
        }

        foreach (Entity entity in _entities.Query<GrassPatch>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            if (screenBounds.Intersects(PlayfieldRenderer.GrassScreenBounds(entity.GetComponent<GrassPatch>(), GroupOffset.View(entity, view))))
                selected.Add(entity);
        }

        foreach (Entity entity in _entities.Query<LevelPolygon>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            if (screenBounds.Intersects(PlayfieldRenderer.PolygonScreenBounds(entity.GetComponent<LevelPolygon>(), GroupOffset.View(entity, view))))
                selected.Add(entity);
        }

        foreach (Entity entity in _entities.Query<Spike>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            Spike spike = entity.GetComponent<Spike>();
            var spikeBounds = new Rect2(
                view.PlayfieldToScreen(GroupOffset.Shown(entity, spike.Position)),
                spike.Size * view.Scale);
            if (screenBounds.Intersects(spikeBounds))
                selected.Add(entity);
        }

        foreach (Entity entity in _entities.Query<Block>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            Block block = entity.GetComponent<Block>();
            var blockBounds = new Rect2(
                view.PlayfieldToScreen(GroupOffset.Shown(entity, block.Position)),
                block.Size * view.Scale);
            if (screenBounds.Intersects(blockBounds))
                selected.Add(entity);
        }

        foreach (Entity entity in _entities.Query<ScreenReadRegion>().ToEntityList())
        {
            if (!layers.Picks(ItemLayer.Of(entity).Layer))
                continue;

            ScreenReadRegion region = entity.GetComponent<ScreenReadRegion>();
            var regionBounds = new Rect2(
                view.PlayfieldToScreen(GroupOffset.Shown(entity, region.Position)),
                region.Size * view.Scale);
            if (screenBounds.Intersects(regionBounds))
                selected.Add(entity);
        }

        return selected;
    }

    /// <summary>The editor's layers: items on hidden or locked ones can't be picked.</summary>
    private EditorLayers Layers() => EditorLayers.Of(scene.Editor());

    /// <summary>True when a playfield point is inside the field.</summary>
    public static bool IsInside(Vector2 position, Playfield playfield) =>
        position.X >= 0 &&
        position.Y >= 0 &&
        position.X <= playfield.Width &&
        position.Y <= playfield.Height;

    /// <summary>
    /// A picked point moved into the item's placed frame: items are picked where
    /// their groups show them at the playhead (docs/render-architecture.md step 3a).
    /// </summary>
    private static Vector2 Placed(Entity entity, Vector2 shown) => shown - GroupOffset.Of(entity);

    /// <summary>Whether a point lies within a radius in both axes of an item's centre.</summary>
    private static bool Within(Vector2 point, Vector2 target, float radius) =>
        MathF.Abs(target.X - point.X) <= radius &&
        MathF.Abs(target.Y - point.Y) <= radius;
}
