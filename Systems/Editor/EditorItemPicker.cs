using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Picks the playfield item under a point and the items inside a box, asking
/// each kind in <see cref="ItemKinds.PickOrder"/> whether the point is on it. Items are found where they're shown at the playhead, with
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
    /// always grabs it. Then each kind in <see cref="ItemKinds.PickOrder"/>,
    /// the last-made hit of a kind being the one on top.
    /// </summary>
    public Entity FindItemAt(Vector2 position, long playheadMs)
    {
        Entity hit = FindSelectedMarkAt(position);
        if (!hit.IsNull)
            return hit;

        EditorLayers layers = Layers();
        foreach (ItemKind kind in ItemKinds.PickOrder)
        {
            Entity result = default;
            foreach (Entity entity in kind.All(_entities))
            {
                if (layers.Picks(ItemLayer.Of(entity).Layer) && kind.Hit(entity, Placed(entity, position), playheadMs))
                    result = entity;
            }

            if (!result.IsNull)
                return result;
        }

        return default;
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
        foreach (ItemKind kind in ItemKinds.All)
        {
            foreach (Entity entity in kind.All(_entities))
            {
                if (layers.Picks(ItemLayer.Of(entity).Layer) && kind.InMarquee(entity, screenBounds, view, playfield, playheadMs))
                    selected.Add(entity);
            }
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
