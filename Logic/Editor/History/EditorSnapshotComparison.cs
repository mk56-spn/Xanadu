using System.Linq;

namespace Xanadu.Logic.Editor.History;

/// <summary>Whether two snapshots describe the same level, which decides if an edit is undoable.</summary>
public static class EditorSnapshotComparison
{
    public static bool AreEqual(EditorSnapshot before, EditorSnapshot after) =>
        before.Items.SequenceEqual(after.Items) && SameGroups(before, after);

    private static bool SameGroups(EditorSnapshot before, EditorSnapshot after)
    {
        if (before.Groups is null || after.Groups is null)
            return before.Groups is null == after.Groups is null;
        if (before.Groups.Count != after.Groups.Count)
            return false;
        for (int i = 0; i < before.Groups.Count; i++)
        {
            if (!before.Groups[i].Same(after.Groups[i]))
                return false;
        }
        return true;
    }
}
