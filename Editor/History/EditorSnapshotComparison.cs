using System.Collections.Generic;
using System.Linq;
using Xanadu.Ecs;
using Xanadu.Levels;

namespace Xanadu.Editor;

/// <summary>Whether two snapshots describe the same level, which decides if an edit is undoable.</summary>
public static class EditorSnapshotComparison
{
    public static bool AreEqual(EditorSnapshot before, EditorSnapshot after) =>
        before.Notes.SequenceEqual(after.Notes) &&
        before.Mines.SequenceEqual(after.Mines) &&
        before.StartPoints.SequenceEqual(after.StartPoints) &&
        before.Blocks.SequenceEqual(after.Blocks) &&
        before.Spikes.SequenceEqual(after.Spikes) &&
        (before.Texts ?? new()).SequenceEqual(after.Texts ?? new()) &&
        (before.Grass ?? new()).SequenceEqual(after.Grass ?? new()) &&
        SamePolygons(before.Polygons, after.Polygons) &&
        SameGroups(before.Groups, after.Groups);

    /// <summary>Polygon snapshots hold items by reference, so they compare by content.</summary>
    private static bool SamePolygons(List<PolygonSnapshot>? a, List<PolygonSnapshot>? b)
    {
        a ??= new();
        b ??= new();
        if (a.Count != b.Count)
            return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] with { Item = null! } != b[i] with { Item = null! } ||
                !Xanadu.Items.XifCopy.AreEqual(a[i].Item, b[i].Item))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameGroups(GroupDefinition[]? a, GroupDefinition[]? b)
    {
        if (a is null || b is null)
            return a is null && b is null;
        return new LevelGroups { Groups = a }.SameAs(new LevelGroups { Groups = b });
    }
}
