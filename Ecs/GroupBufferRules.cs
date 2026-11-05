using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>What one item will have after an edit that has not been made yet.</summary>
public readonly record struct BufferPreview(int Layer, uint Mask);

/// <summary>
/// When a group may be buffered (docs/render-architecture.md D15/D16): every
/// member on one layer, and no member in any other group. Buffering is a lock
/// (D16a), so the editor asks this before it turns the flag on and refuses any
/// later edit that would break one of the rules.
/// </summary>
public static class GroupBufferRules
{
    public const string OneLayer = "buffered groups must sit on one layer";
    public const string OneGroup = "items in a buffered group can't be in other groups";

    /// <summary>Why the group can't be buffered, or null when it can.</summary>
    public static string? Problem(EntityStore store, IReadOnlyList<GroupDefinition> groups, int group) =>
        Problem(store, groups, group, null);

    /// <summary>
    /// Why the group can't be buffered, or null when it can, where
    /// <paramref name="after"/> gives the layer and group mask an item will have
    /// once a pending edit is made. Items not in it are read as they are.
    /// </summary>
    public static string? Problem(
        EntityStore store,
        IReadOnlyList<GroupDefinition> groups,
        int group,
        IReadOnlyDictionary<int, BufferPreview>? after)
    {
        if (group < 0 || group >= groups.Count || group >= GroupLimits.Count)
            return OneGroup;

        uint bit = ItemGroups.Bit(group);
        int layer = 0;
        bool any = false;
        var seen = new HashSet<int>();
        foreach (Entity entity in store.Query<ItemGroups>().Entities)
        {
            seen.Add(entity.Id);
            ItemGroups member = entity.GetComponent<ItemGroups>();
            int on = ItemLayer.Of(entity).Layer;
            uint mask = member.Mask;
            if (after is not null && after.TryGetValue(entity.Id, out BufferPreview pending))
            {
                on = pending.Layer;
                mask = pending.Mask;
            }

            if ((mask & bit) == 0)
                continue;
            if (ItemGroups.Count(mask) > 1)
                return OneGroup;

            if (!any)
            {
                layer = on;
                any = true;
            }
            else if (on != layer)
            {
                return OneLayer;
            }
        }

        if (after is null)
            return null;

        // An item that is joining a group may not carry ItemGroups yet, so it
        // is not in the query above.
        foreach ((int id, BufferPreview pending) in after)
        {
            if (seen.Contains(id) || (pending.Mask & bit) == 0)
                continue;
            if (ItemGroups.Count(pending.Mask) > 1)
                return OneGroup;
            if (!any)
            {
                layer = pending.Layer;
                any = true;
            }
            else if (pending.Layer != layer)
            {
                return OneLayer;
            }
        }

        return null;
    }

    /// <summary>Whether the group may be buffered now.</summary>
    public static bool CanBuffer(EntityStore store, IReadOnlyList<GroupDefinition> groups, int group) =>
        Problem(store, groups, group) is null;

    /// <summary>
    /// The first buffered group an edit has broken, with the message to show,
    /// or null when every buffered group still holds its rules.
    /// </summary>
    public static string? Broken(EntityStore store, IReadOnlyList<GroupDefinition> groups) =>
        Broken(store, groups, null);

    /// <summary>
    /// The first buffered group a pending edit would break, with the message to
    /// show, or null when every buffered group would still hold its rules.
    /// </summary>
    public static string? Broken(
        EntityStore store,
        IReadOnlyList<GroupDefinition> groups,
        IReadOnlyDictionary<int, BufferPreview>? after)
    {
        for (int i = 0; i < groups.Count && i < GroupLimits.Count; i++)
        {
            if (!groups[i].Buffered)
                continue;
            string? problem = Problem(store, groups, i, after);
            if (problem is not null)
                return problem;
        }

        return null;
    }
}