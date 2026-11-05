using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The buffer lock's edit guard (docs/render-architecture.md D16a): an edit
/// that would break a buffered group is refused before it is made, with the
/// same message a warning popup shows. Every editor command that changes a
/// member's layer or its group mask asks this first.
/// </summary>
public static class EditorBufferGuard
{
    /// <summary>
    /// Why moving <paramref name="items"/> to <paramref name="layer"/> would
    /// break a buffered group, or null when it is safe. Moving every member of
    /// a group together keeps it on one layer and is allowed.
    /// </summary>
    public static string? MovingToLayer(EntityStore store, IReadOnlyList<Entity> items, int layer)
    {
        GroupDefinition[]? groups = Groups(store);
        if (groups is null || NoBuffer(groups))
            return null;

        var after = new Dictionary<int, BufferPreview>(items.Count);
        foreach (Entity item in items)
        {
            if (!EditorItems.IsItem(item))
                continue;
            uint mask = item.TryGetComponent(out ItemGroups current) ? current.Mask : 0u;
            after[item.Id] = new BufferPreview(layer, mask);
        }

        return GroupBufferRules.Broken(store, groups, after);
    }

    /// <summary>
    /// Why putting <paramref name="items"/> into <paramref name="group"/> would
    /// break a buffered group, or null when it is safe: a buffered group's
    /// members all have to stay on its layer and in no other group.
    /// </summary>
    public static string? AddingToGroup(EntityStore store, IReadOnlyList<Entity> items, int group)
    {
        GroupDefinition[]? groups = Groups(store);
        if (groups is null || NoBuffer(groups) || group < 0 || group >= groups.Length)
            return null;

        uint bit = ItemGroups.Bit(group);
        var after = new Dictionary<int, BufferPreview>(items.Count);
        foreach (Entity item in items)
        {
            if (!EditorItems.IsItem(item))
                continue;
            uint mask = item.TryGetComponent(out ItemGroups current) ? current.Mask : 0u;
            after[item.Id] = new BufferPreview(ItemLayer.Of(item).Layer, mask | bit);
        }

        return GroupBufferRules.Broken(store, groups, after);
    }

    private static GroupDefinition[]? Groups(EntityStore store)
    {
        foreach (Entity entity in store.Query<LevelGroups>().Entities)
            return entity.GetComponent<LevelGroups>().Groups;
        return null;
    }

    private static bool NoBuffer(GroupDefinition[] groups)
    {
        for (int i = 0; i < groups.Length && i < GroupLimits.Count; i++)
        {
            if (groups[i].Buffered)
                return false;
        }

        return true;
    }
}
