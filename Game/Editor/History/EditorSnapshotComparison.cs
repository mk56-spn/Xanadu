using System.Linq;
using Xanadu.Ecs;
using Xanadu.Levels;

namespace Xanadu.Editor;

/// <summary>Whether two snapshots describe the same level, which decides if an edit is undoable.</summary>
public static class EditorSnapshotComparison
{
    public static bool AreEqual(EditorSnapshot before, EditorSnapshot after) =>
        before.Items.SequenceEqual(after.Items) &&
        SameGroups(before.Groups, after.Groups) &&
        SameLayers(before.Layers, after.Layers);

    private static bool SameLayers(LevelLayers? a, LevelLayers? b)
    {
        if (a is null || b is null)
            return a is null && b is null;
        return a.Value.SameAs(b.Value);
    }

    private static bool SameGroups(GroupDefinition[]? a, GroupDefinition[]? b)
    {
        if (a is null || b is null)
            return a is null && b is null;
        return new LevelGroups { Groups = a }.SameAs(new LevelGroups { Groups = b });
    }
}
