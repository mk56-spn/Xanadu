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
/// under everything.
/// </summary>
public sealed class EditorItemPicker(EditorSceneQuery scene)
{
    private const float NoteHitRadius = PlayfieldRenderer.NoteSize / 2;
    private const float MineHitRadius = PlayfieldRenderer.MineSize / 2;

    private readonly EntityStore _entities = scene.Store;

    /// <summary>The topmost item a press at this point would pick, or null.</summary>
    public Entity FindItemAt(Vector2 position, long playheadMs)
    {
        Entity hit = FindNoteAt(position, playheadMs);
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
        return hit;
    }

    public Entity FindBlockAt(Vector2 position)
    {
        Entity result = default;
        foreach (Entity entity in _entities.Query<Block>().ToEntityList())
        {
            if (entity.GetComponent<Block>().Bounds.HasPoint(position))
                result = entity;
        }

        return result;
    }

    public Entity FindTextAt(Vector2 position)
    {
        Entity result = default;
        foreach (Entity entity in _entities.Query<LevelText>().ToEntityList())
        {
            LevelText text = entity.GetComponent<LevelText>();
            if (TurnedBox.Contains(text.Bounds, text.RotationDegrees, position))
                result = entity;
        }

        return result;
    }

    public Entity FindGrassAt(Vector2 position)
    {
        Entity result = default;
        foreach (Entity entity in _entities.Query<GrassPatch>().ToEntityList())
        {
            GrassPatch grass = entity.GetComponent<GrassPatch>();
            if (TurnedBox.Contains(grass.Bounds, grass.RotationDegrees, position))
                result = entity;
        }

        return result;
    }

    public Entity FindPolygonAt(Vector2 position)
    {
        Entity result = default;
        foreach (Entity entity in _entities.Query<LevelPolygon>().ToEntityList())
        {
            if (entity.GetComponent<LevelPolygon>().Contains(position))
                result = entity;
        }

        return result;
    }

    public Entity FindSpikeAt(Vector2 position)
    {
        Entity result = default;
        foreach (Entity entity in _entities.Query<Spike>().ToEntityList())
        {
            if (entity.GetComponent<Spike>().Contains(position))
                result = entity;
        }

        return result;
    }

    public Entity FindNoteAt(Vector2 position, long playheadMs)
    {
        Entity result = default;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (NoteFade.Alpha(note, playheadMs) <= 0)
                continue;

            if (Within(position, note.Position, NoteHitRadius))
            {
                result = entity;
            }
        }

        return result;
    }

    public Entity FindMineAt(Vector2 position, long playheadMs)
    {
        Entity result = default;

        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
        {
            Mine mine = entity.GetComponent<Mine>();
            if (MineFade.Alpha(mine, playheadMs) <= 0)
                continue;

            if (Within(position, mine.Position, MineHitRadius))
            {
                result = entity;
            }
        }

        return result;
    }

    public Entity FindStartAt(Vector2 position)
    {
        Entity result = default;
        foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
        {
            StartPoint startPoint = entity.GetComponent<StartPoint>();
            // Picked like a note, since it is drawn the size of one.
            if (startPoint.Position.DistanceTo(position) <= NoteHitRadius)
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

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        float noteSize = PlayfieldRenderer.NoteSize * view.Scale;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (NoteRules.IsRail(note.Kind) ||
                NoteRules.IsChannelEnd(note.Kind) ||
                NoteFade.Alpha(note, playheadMs) <= 0.05f ||
                !IsInside(note.Position, playfield))
            {
                continue;
            }

            Vector2 center = view.PlayfieldToScreen(note.Position);
            var noteBounds = new Rect2(
                center - new Vector2(noteSize / 2, noteSize / 2),
                new Vector2(noteSize, noteSize));
            if (screenBounds.Intersects(noteBounds))
                selected.Add(entity);
        }

        float mineSize = PlayfieldRenderer.MineSize * view.Scale;
        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
        {
            Mine mine = entity.GetComponent<Mine>();
            if (MineFade.Alpha(mine, playheadMs) <= 0.05f ||
                !IsInside(mine.Position, playfield))
            {
                continue;
            }

            Vector2 center = view.PlayfieldToScreen(mine.Position);
            var mineBounds = new Rect2(
                center - new Vector2(mineSize / 2, mineSize / 2),
                new Vector2(mineSize, mineSize));
            if (screenBounds.Intersects(mineBounds))
                selected.Add(entity);
        }

        foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
        {
            StartPoint start = entity.GetComponent<StartPoint>();
            Vector2 center = view.PlayfieldToScreen(start.Position);
            if (screenBounds.HasPoint(center))
                selected.Add(entity);
        }

        foreach (Entity entity in _entities.Query<LevelText>().ToEntityList())
        {
            if (screenBounds.Intersects(PlayfieldRenderer.TextScreenHull(entity.GetComponent<LevelText>(), view)))
                selected.Add(entity);
        }

        foreach (Entity entity in _entities.Query<GrassPatch>().ToEntityList())
        {
            if (screenBounds.Intersects(PlayfieldRenderer.GrassScreenBounds(entity.GetComponent<GrassPatch>(), view)))
                selected.Add(entity);
        }

        foreach (Entity entity in _entities.Query<LevelPolygon>().ToEntityList())
        {
            if (screenBounds.Intersects(PlayfieldRenderer.PolygonScreenBounds(entity.GetComponent<LevelPolygon>(), view)))
                selected.Add(entity);
        }

        foreach (Entity entity in _entities.Query<Spike>().ToEntityList())
        {
            Spike spike = entity.GetComponent<Spike>();
            var spikeBounds = new Rect2(
                view.PlayfieldToScreen(spike.Position),
                spike.Size * view.Scale);
            if (screenBounds.Intersects(spikeBounds))
                selected.Add(entity);
        }

        foreach (Entity entity in _entities.Query<Block>().ToEntityList())
        {
            Block block = entity.GetComponent<Block>();
            var blockBounds = new Rect2(
                view.PlayfieldToScreen(block.Position),
                block.Size * view.Scale);
            if (screenBounds.Intersects(blockBounds))
                selected.Add(entity);
        }

        return selected;
    }

    /// <summary>True when a playfield point is inside the field.</summary>
    public static bool IsInside(Vector2 position, Playfield playfield) =>
        position.X >= 0 &&
        position.Y >= 0 &&
        position.X <= playfield.Width &&
        position.Y <= playfield.Height;

    /// <summary>Whether a point lies within a radius in both axes of an item's centre.</summary>
    private static bool Within(Vector2 point, Vector2 target, float radius) =>
        MathF.Abs(target.X - point.X) <= radius &&
        MathF.Abs(target.Y - point.Y) <= radius;
}
