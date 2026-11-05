using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Layering;
using Xanadu.Systems.Playfields;
using Xanadu.Rendering;

namespace Xanadu.Systems.Editor;

/// <summary>One row of the layers panel: a layer and how many items are on it. Layer 0 is the player wedge.</summary>
public readonly record struct EditorLayerRow(int Layer, int Count)
{
    public bool IsWedge => Layer == 0;
}

/// <summary>
/// The level editor's layer edits (docs/render-architecture.md step 4): the
/// layers panel's rows, adding a layer beside a row, putting items on another
/// layer, stepping z, and moving items forward or back through their layer
/// with the shared <see cref="DrawStack"/>. The callers wrap each edit in one
/// undo step.
/// </summary>
public static class EditorLayerEdits
{
    /// <summary>
    /// Every layer in use, added from the panel or in the level's layer table,
    /// plus the player wedge, front first.
    /// </summary>
    public static List<EditorLayerRow> Rows(EntityStore store, in EditorLayers layers, in LevelLayers table)
    {
        var counts = new Dictionary<int, int> { [0] = 0 };
        foreach (Entity item in Items(store))
        {
            int layer = item.GetComponent<ItemLayer>().Layer;
            counts[layer] = counts.TryGetValue(layer, out int count) ? count + 1 : 1;
        }

        if (layers.Kept is not null)
        {
            foreach (int layer in layers.Kept)
                counts.TryAdd(layer, 0);
        }

        if (table.Layers is not null)
        {
            foreach (int layer in table.Layers.Keys)
                counts.TryAdd(layer, 0);
        }

        var rows = new List<EditorLayerRow>(counts.Count);
        foreach ((int layer, int count) in counts)
            rows.Add(new EditorLayerRow(layer, count));
        rows.Sort((a, b) => b.Layer.CompareTo(a.Layer));
        return rows;
    }

    /// <summary>The placed items on one layer.</summary>
    public static List<Entity> OnLayer(EntityStore store, int layer)
    {
        var found = new List<Entity>();
        foreach (Entity item in Items(store))
        {
            if (item.GetComponent<ItemLayer>().Layer == layer)
                found.Add(item);
        }

        return found;
    }

    /// <summary>
    /// Adds an empty layer just in front of (<paramref name="front"/>) or behind
    /// the row <paramref name="beside"/>, makes it current and returns its
    /// number. A free number between the two neighbours is used as it is;
    /// otherwise the layers on the far side, with their items and layer table
    /// entries, move one further from the wedge to make room. 0 when that would
    /// push a layer past the range, in which case nothing changes.
    /// </summary>
    public static int Insert(EntityStore store, ref EditorLayers layers, ref LevelLayers table, int beside, bool front)
    {
        HashSet<int> used = Used(store, layers, table);
        int candidate = beside + (front ? 1 : -1);
        int added;
        if (candidate != 0 && !used.Contains(candidate) && DrawOrder.ClampLayer(candidate) == candidate)
        {
            added = candidate;
        }
        else
        {
            // The new layer takes whichever of the two numbers is further from
            // the wedge, and everything from there outwards steps out by one.
            added = beside == 0 || Math.Abs(candidate) > Math.Abs(beside) ? candidate : beside;
            int side = Math.Sign(added);
            int furthest = 0;
            foreach (int layer in used)
            {
                if (Outward(layer, added))
                    furthest = Math.Max(furthest, Math.Abs(layer));
            }

            if (furthest + 1 > DrawOrder.MaxLayer || Math.Abs(added) > DrawOrder.MaxLayer)
                return 0;

            Shift(store, ref layers, ref table, added, side);
        }

        layers.Kept ??= new HashSet<int>();
        layers.Kept.Add(added);
        layers.Current = added;
        return added;
    }

    /// <summary>One layer further forward (+1) or back (-1), stepping over the wedge's 0 and staying in range.</summary>
    public static int StepLayer(int layer, int step)
    {
        int next = layer + Math.Sign(step);
        if (next == 0)
            next += Math.Sign(step);
        return next == 0 ? layer : DrawOrder.ClampLayer(next);
    }

    /// <summary>
    /// Puts items on <paramref name="layer"/>, in front of what is already
    /// there and in their own order among themselves. False when every item
    /// was already on it.
    /// </summary>
    public static bool PutOnLayer(EntityStore store, IReadOnlyList<Entity> items, int layer)
    {
        var moving = new List<Entity>();
        foreach (Entity item in items)
        {
            if (EditorItems.IsItem(item) && ItemLayer.Of(item).Layer != layer)
                moving.Add(item);
        }

        if (moving.Count == 0 || layer == 0)
            return false;

        foreach (Entity item in moving)
            ItemLayer.Put(item, ItemLayer.Of(item) with { Layer = layer });
        ItemLayer.OnTop(store, moving);
        return true;
    }

    /// <summary>Changes every item's z by <paramref name="step"/>.</summary>
    public static bool StepZ(IReadOnlyList<Entity> items, int step)
    {
        bool changed = false;
        foreach (Entity item in items)
        {
            if (!EditorItems.IsItem(item) || step == 0)
                continue;
            ItemLayer layer = ItemLayer.Of(item);
            ItemLayer.Put(item, layer with { Z = layer.Z + step });
            changed = true;
        }

        return changed;
    }

    /// <summary>Puts every selected item on one blend mode; false when they already all are.</summary>
    public static bool SetBlend(IReadOnlyList<Entity> items, BlendMode mode)
    {
        bool changed = false;
        foreach (Entity item in items)
        {
            if (!EditorItems.IsItem(item) || ItemBlend.Of(item) == mode)
                continue;
            ItemBlend.Put(item, mode);
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Steps where the selection's shader comes from: its own shader, or one of
    /// the groups it is in, in slot order and wrapping. Items without the group
    /// are left on their own shader, since a source must be in the mask.
    /// </summary>
    public static bool StepShaderSource(IReadOnlyList<Entity> items, int step)
    {
        if (step == 0)
            return false;

        Entity first = default;
        foreach (Entity item in items)
        {
            if (EditorItems.IsItem(item))
            {
                first = item;
                break;
            }
        }

        if (first.IsNull)
            return false;

        var options = new System.Collections.Generic.List<int> { ItemShaderSource.Own };
        if (first.TryGetComponent(out ItemGroups mask))
        {
            for (int group = 0; group < GroupLimits.Count; group++)
            {
                if (mask.Has(group))
                    options.Add(group);
            }
        }

        int current = ItemShaderSource.Of(first);
        int index = options.IndexOf(current);
        if (index < 0)
            index = 0;
        int next = options[((index + step) % options.Count + options.Count) % options.Count];

        bool changed = false;
        foreach (Entity item in items)
        {
            if (!EditorItems.IsItem(item))
                continue;
            if (next != ItemShaderSource.Own &&
                (!item.TryGetComponent(out ItemGroups itemMask) || !itemMask.Has(next)))
            {
                continue;
            }

            if (ItemShaderSource.Of(item) == next)
                continue;
            ItemShaderSource.Put(item, next);
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Moves each item one place forward (+1) or back (-1) through the draw
    /// order of its own layer: it swaps z with the item it passes, or steps
    /// past it when they're tied. An item never leaves its layer, and never
    /// passes another moving item, so a selection keeps its own order.
    /// False when nothing could move.
    /// </summary>
    public static bool Move(EntityStore store, IReadOnlyList<Entity> items, int direction)
    {
        var moving = new HashSet<int>();
        foreach (Entity item in items)
        {
            if (EditorItems.IsItem(item))
                moving.Add(item.Id);
        }

        if (moving.Count == 0 || direction == 0)
            return false;

        // The front-most mover goes first going forward, the back-most going back.
        List<Entity> stack = FrontFirst(store);
        var order = new List<Entity>();
        foreach (Entity entity in stack)
        {
            if (moving.Contains(entity.Id))
                order.Add(entity);
        }

        if (direction < 0)
            order.Reverse();

        bool changed = false;
        foreach (Entity item in order)
        {
            stack = FrontFirst(store);
            int at = stack.FindIndex(entity => entity.Id == item.Id);
            int next = DrawStack.Neighbour(at, direction, stack.Count);
            if (next < 0 || moving.Contains(stack[next].Id))
                continue;

            Entity neighbour = stack[next];
            if (!DrawStack.TryMove(
                    Slot(item, stack.Count - at),
                    Slot(neighbour, stack.Count - next),
                    direction,
                    DrawStackRule.ZInLayer,
                    out DrawSlot movedItem,
                    out DrawSlot movedNeighbour))
            {
                continue;
            }

            ItemLayer.Put(neighbour, new ItemLayer(movedNeighbour.Layer, movedNeighbour.Z));
            ItemLayer.Put(item, new ItemLayer(movedItem.Layer, movedItem.Z));
            changed = true;
        }

        return changed;
    }

    /// <summary>Every placed item, front first, the way they draw.</summary>
    public static List<Entity> FrontFirst(EntityStore store)
    {
        var entries = new List<PlayfieldDrawEntry>();
        PlayfieldDrawList.Gather(store, entries, startPoints: true, railLanes: false);
        var items = new List<Entity>(entries.Count);
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entity entity = entries[i].Entity;
            if (EditorItems.IsItem(entity))
                items.Add(entity);
        }

        return items;
    }

    private static DrawSlot Slot(Entity item, int rank)
    {
        ItemLayer layer = ItemLayer.Of(item);
        return new DrawSlot(layer.Layer, layer.Z, rank);
    }

    private static List<Entity> Items(EntityStore store)
    {
        var items = new List<Entity>();
        foreach (Entity entity in store.Query<ItemLayer>().Entities)
        {
            if (EditorItems.IsItem(entity))
                items.Add(entity);
        }

        return items;
    }

    private static HashSet<int> Used(EntityStore store, in EditorLayers layers, in LevelLayers table)
    {
        var used = new HashSet<int>();
        foreach (EditorLayerRow row in Rows(store, layers, table))
        {
            if (!row.IsWedge)
                used.Add(row.Layer);
        }

        return used;
    }

    /// <summary>Whether <paramref name="layer"/> is <paramref name="from"/> or further from the wedge on its side.</summary>
    private static bool Outward(int layer, int from) =>
        Math.Sign(layer) == Math.Sign(from) && Math.Abs(layer) >= Math.Abs(from);

    /// <summary>Steps every layer from <paramref name="from"/> outwards one further out: items, table entries and the editor's own state.</summary>
    private static void Shift(EntityStore store, ref EditorLayers layers, ref LevelLayers table, int from, int side)
    {
        foreach (Entity item in Items(store))
        {
            ItemLayer layer = item.GetComponent<ItemLayer>();
            if (Outward(layer.Layer, from))
                item.GetComponent<ItemLayer>() = layer with { Layer = layer.Layer + side };
        }

        int Moved(int layer) => Outward(layer, from) ? layer + side : layer;
        layers.Hidden = ShiftSet(layers.Hidden, Moved);
        layers.Locked = ShiftSet(layers.Locked, Moved);
        layers.Kept = ShiftSet(layers.Kept, Moved);
        table.Renumber(Moved);

        if (layers.Solo != 0)
            layers.Solo = Moved(layers.Solo);
        layers.Current = Moved(layers.Current);
    }

    private static HashSet<int> ShiftSet(HashSet<int>? set, Func<int, int> moved)
    {
        var shifted = new HashSet<int>();
        if (set is null)
            return shifted;
        foreach (int layer in set)
            shifted.Add(moved(layer));
        return shifted;
    }
}
