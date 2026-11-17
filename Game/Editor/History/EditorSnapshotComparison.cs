using System.Linq;

namespace Xanadu.Editor.History;

/// <summary>Whether two snapshots describe the same level, which decides if an edit is undoable.</summary>
public static class EditorSnapshotComparison
{
    public static bool AreEqual(EditorSnapshot before, EditorSnapshot after) =>
        before.Items.SequenceEqual(after.Items);
}
