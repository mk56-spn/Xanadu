using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;

namespace Writer.Systems.Playfields;

/// <summary>
/// Applies the level's groups while a play scene runs. Every note, mine and
/// block in a group is placed at its own position plus the sum of its groups'
/// moves, so the moved position is the real one: hits, mines and collisions all
/// read it. Their colours are faded towards the groups' colour effects.
/// The editor keeps items where they were placed; it never runs this.
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
        if (!TryFindLevel(out LevelGroups levelGroups, out Playhead playhead))
            return;

        float seconds = playhead.TimeMs / 1000f;
        // Adding components to items is not allowed mid-query, so work from a copy.
        foreach (Entity entity in Query.ToEntityList())
            Apply(entity, levelGroups.Groups, seconds);
    }

    private static void Apply(Entity entity, GroupDefinition[] groups, float seconds)
    {
        uint mask = entity.GetComponent<ItemGroups>().Mask;
        if (mask == 0)
            return;

        if (entity.HasComponent<Note>())
        {
            Vector2 basePosition = BaseOf(entity, entity.GetComponent<Note>().Position);
            entity.GetComponent<Note>().Position = basePosition + GroupModulation.Offset(groups, mask, seconds);
        }
        else if (entity.HasComponent<Mine>())
        {
            Vector2 basePosition = BaseOf(entity, entity.GetComponent<Mine>().Position);
            entity.GetComponent<Mine>().Position = basePosition + GroupModulation.Offset(groups, mask, seconds);
        }
        else if (entity.HasComponent<Block>())
        {
            Vector2 basePosition = BaseOf(entity, entity.GetComponent<Block>().Position);
            entity.GetComponent<Block>().Position = basePosition + GroupModulation.Offset(groups, mask, seconds);
        }
        else if (entity.HasComponent<StartPoint>())
        {
            Vector2 basePosition = BaseOf(entity, entity.GetComponent<StartPoint>().Position);
            entity.GetComponent<StartPoint>().Position = basePosition + GroupModulation.Offset(groups, mask, seconds);
        }
        else
        {
            return;
        }

        GroupModulation.TryColor(groups, mask, seconds, out Color mix, out float coverage);
        if (entity.HasComponent<GroupColor>())
            entity.GetComponent<GroupColor>() = new GroupColor(mix, coverage);
        else
            entity.AddComponent(new GroupColor(mix, coverage));
    }

    /// <summary>Remembers where an item was first seen, which is where it was placed.</summary>
    private static Vector2 BaseOf(Entity entity, Vector2 current)
    {
        if (entity.TryGetComponent(out GroupBase known))
            return known.Position;

        entity.AddComponent(new GroupBase(current));
        return current;
    }

    /// <summary>The play scene's level groups and transport; the editor does not move its items.</summary>
    private bool TryFindLevel(out LevelGroups groups, out Playhead playhead)
    {
        foreach (Entity entity in _entities.Query<LevelGroups, Playhead, PlayfieldMode>().ToEntityList())
        {
            if (entity.GetComponent<PlayfieldMode>().Kind != PlayfieldKind.Play)
                continue;

            groups = entity.GetComponent<LevelGroups>();
            playhead = entity.GetComponent<Playhead>();
            return true;
        }

        groups = default;
        playhead = default;
        return false;
    }
}
