using Friflo.Engine.ECS;
using Xanadu.Ecs;

namespace Xanadu.Editor;

/// <summary>Rebuilds the level's editable items from a snapshot when undo or redo lands.</summary>
public sealed class EditorSnapshotRestore(EntityStore entities)
{
    public void Apply(EditorSnapshot snapshot)
    {
        foreach (ItemKind kind in ItemKinds.All)
            kind.DeleteAll(entities);

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

        // Restore detaches each value again, so later edits never reach the history.
        foreach (ItemCopy item in snapshot.Items)
            item.Kind.Restore(entities, item);
    }
}
