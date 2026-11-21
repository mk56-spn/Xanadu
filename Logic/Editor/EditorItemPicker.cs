using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Model.Editor;
using Xanadu.Logic.Editor.Kinds;

namespace Xanadu.Logic.Editor;

/// <summary>
/// Picks the playfield item under a point and the items inside a box, asking
/// each kind in <see cref="ItemKinds.PickOrder"/> whether the point is on it.
/// </summary>
public sealed class EditorItemPicker(EditorSceneQuery scene)
{
    private const float NoteHitRadius = PlayfieldMetrics.NoteSize / 2;

    private readonly EntityStore _entities = scene.Store;

    /// <summary>
    /// The topmost item a press at this point would pick, or null. A selected
    /// note comes first and is found even when faded out at the playhead, since
    /// its selection ring still shows: a press inside the ring always grabs it.
    /// Then each kind in <see cref="ItemKinds.PickOrder"/>, the last-made hit of
    /// a kind being the one on top.
    /// </summary>
    public Entity FindItemAt(Vector2 position, long playheadMs)
    {
        Entity hit = FindSelectedMarkAt(position);
        if (!hit.IsNull)
            return hit;

        foreach (ItemKind kind in ItemKinds.PickOrder)
        {
            Entity result = default;
            foreach (Entity entity in kind.All(_entities))
            {
                if (kind.Hit(entity, position, playheadMs))
                    result = entity;
            }

            if (!result.IsNull)
                return result;
        }

        return default;
    }

    /// <summary>The selected note under a point, whatever its fade, or null.</summary>
    public Entity FindSelectedMarkAt(Vector2 position)
    {
        Entity editor = scene.Editor();
        if (editor.IsNull || !editor.HasComponent<NoteSelection>())
            return default;

        Entity result = default;
        foreach (Entity entity in editor.GetComponent<NoteSelection>().AllNotes)
        {
            if (EditorItems.IsItem(entity) &&
                entity.TryGetComponent(out Note note) &&
                Within(position, note.Position, NoteHitRadius))
            {
                result = entity;
            }
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
        foreach (ItemKind kind in ItemKinds.All)
        {
            foreach (Entity entity in kind.All(_entities))
            {
                if (kind.InMarquee(entity, screenBounds, view, playfield, playheadMs))
                    selected.Add(entity);
            }
        }

        return selected;
    }

    /// <summary>Whether a point lies within a radius in both axes of an item's centre.</summary>
    private static bool Within(Vector2 point, Vector2 target, float radius) =>
        MathF.Abs(target.X - point.X) <= radius &&
        MathF.Abs(target.Y - point.Y) <= radius;
}
