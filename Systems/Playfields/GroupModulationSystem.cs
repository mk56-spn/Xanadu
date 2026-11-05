using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Works out what the level's groups do to each member at the playhead, in play
/// and in the editor alike (docs/render-architecture.md step 3a): how far its
/// moves have taken it (<see cref="GroupOffset"/>) and the colour its fades
/// reach (<see cref="GroupColor"/>, and <see cref="GroupOutlineColor"/> for a
/// block's outline). Nothing here touches an item's own position: drawing,
/// hits, collisions and editor picking add the offset where they read it, so
/// the placed position is what the level, undo and saving keep.
/// In the editor, a soloed group is the only one applied.
/// </summary>
public sealed class GroupModulationSystem : QuerySystem<ItemGroups>
{
    private readonly EntityStore _entities;

    public GroupModulationSystem(EntityStore entities)
    {
        _entities = entities;
    }

    protected override void OnUpdate()
    {
        if (!TryFindLevel(out GroupDefinition[] groups, out float seconds, out uint solo))
            return;

        CommandBuffer buffer = CommandBuffer;
        Query.ForEachEntity((ref ItemGroups member, Entity entity) =>
        {
            uint mask = member.Mask & solo;
            if (mask == 0)
            {
                Clear(entity, buffer);
                return;
            }

            Set(entity, new GroupOffset(GroupModulation.Offset(groups, mask, seconds)), buffer);

            // A block's interior and outline each follow only the groups linked to them.
            uint colorMask = mask;
            if (entity.TryGetComponent(out Block linked))
            {
                colorMask = mask & linked.FillGroups;
                GroupModulation.TryColor(
                    groups,
                    mask & linked.OutlineGroups,
                    seconds,
                    out Color edgeMix,
                    out float edgeCoverage,
                    out float edgeAlpha);
                Set(entity, new GroupOutlineColor(edgeMix, edgeCoverage, edgeAlpha), buffer);
            }

            GroupModulation.TryColor(
                groups,
                colorMask,
                seconds,
                out Color mix,
                out float coverage,
                out float alpha);
            Set(entity, new GroupColor(mix, coverage, alpha), buffer);
        });
    }

    /// <summary>Writes a value the item already has, or adds it once the query is done.</summary>
    private static void Set<T>(Entity entity, T value, CommandBuffer buffer) where T : struct, IComponent
    {
        if (entity.HasComponent<T>())
            entity.GetComponent<T>() = value;
        else
            buffer.AddComponent(entity.Id, value);
    }

    /// <summary>An item that has left every group (or every applied one) goes back to how it was placed.</summary>
    private static void Clear(Entity entity, CommandBuffer buffer)
    {
        if (entity.HasComponent<GroupOffset>())
            buffer.RemoveComponent<GroupOffset>(entity.Id);
        if (entity.HasComponent<GroupColor>())
            buffer.RemoveComponent<GroupColor>(entity.Id);
        if (entity.HasComponent<GroupOutlineColor>())
            buffer.RemoveComponent<GroupOutlineColor>(entity.Id);
    }

    /// <summary>
    /// The playfield's groups and playhead, play first when both exist.
    /// <paramref name="solo"/> is the mask of groups that apply: every group,
    /// or the one soloed in the editor's group overlay.
    /// </summary>
    private bool TryFindLevel(out GroupDefinition[] groups, out float seconds, out uint solo)
    {
        groups = [];
        seconds = 0f;
        solo = uint.MaxValue;
        bool found = false;
        foreach (Entity entity in _entities.Query<LevelGroups, Playhead, PlayfieldMode>().Entities)
        {
            bool play = entity.GetComponent<PlayfieldMode>().Kind == PlayfieldKind.Play;
            if (found && !play)
                continue;

            groups = entity.GetComponent<LevelGroups>().Groups ?? [];
            seconds = entity.GetComponent<Playhead>().TimeMs / 1000f;
            solo = uint.MaxValue;
            if (!play &&
                entity.TryGetComponent(out EditorGroupState state) &&
                state.SoloGroup is >= 0 and < GroupLimits.Count)
            {
                solo = ItemGroups.Bit(state.SoloGroup);
            }

            found = true;
            if (play)
                break;
        }

        return found;
    }
}
