using Friflo.Engine.ECS;
using Xanadu.Logic.Editor.Kinds;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Logic.Editor.History;

/// <summary>Rebuilds the level's editable items from a snapshot when undo or redo lands.</summary>
public sealed class EditorSnapshotRestore(EntityStore entities)
{
    public void Apply(EditorSnapshot snapshot)
    {
        foreach (ItemKind kind in ItemKinds.All)
            kind.DeleteAll(entities);

        // Restore detaches each value again, so later edits never reach the history.
        foreach (ItemCopy item in snapshot.Items)
            item.Kind.Restore(entities, item);

        if (snapshot.Groups is null)
            return;
        foreach (Entity level in entities.Query<LevelGroups>().Entities)
        {
            ref LevelGroups groups = ref level.GetComponent<LevelGroups>();
            groups.Groups = snapshot.Groups.ConvertAll(group => group.Copy());
            if (level.HasComponent<EditorGroupEdit>())
            {
                ref EditorGroupEdit edit = ref level.GetComponent<EditorGroupEdit>();
                edit.Picked = System.Math.Min(edit.Picked, groups.Groups.Count - 1);
            }
        }
    }
}
