using Friflo.Engine.ECS;
using Xanadu.Model.Kinds;

namespace Xanadu.Editor.History;

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
    }
}
