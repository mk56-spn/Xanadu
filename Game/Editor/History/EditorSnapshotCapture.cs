using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;

namespace Xanadu.Editor;

/// <summary>Reads the level's editable items into a snapshot the undo history can compare.</summary>
public sealed class EditorSnapshotCapture(EntityStore entities)
{
    public EditorSnapshot Capture()
    {
        var items = new List<ItemCopy>();
        var made = new List<(ItemCopy Copy, int Id)>();
        foreach (ItemKind kind in ItemKinds.All)
        {
            made.Clear();
            foreach (Entity entity in kind.All(entities))
                made.Add((kind.Copy(entity), entity.Id));

            // Two captures of the same level must give equal lists, so each kind sorts
            // on its own keys, then by entity id: a restore makes items in list order,
            // so ids keep ties where they were, and swapping two alike items still counts.
            made.Sort(static (a, b) =>
            {
                int order = a.Copy.Kind.Compare(a.Copy.Value, b.Copy.Value);
                return order != 0 ? order : a.Id.CompareTo(b.Id);
            });
            foreach ((ItemCopy copy, _) in made)
                items.Add(copy);
        }

        return new EditorSnapshot(items, CaptureGroups(), CaptureLayers());
    }

    private LevelLayers? CaptureLayers()
    {
        foreach (Entity entity in entities.Query<LevelLayers>().ToEntityList())
            return entity.GetComponent<LevelLayers>().Clone();
        return null;
    }

    private GroupDefinition[]? CaptureGroups()
    {
        foreach (Entity entity in entities.Query<LevelGroups>().ToEntityList())
            return entity.GetComponent<LevelGroups>().Clone().Groups;
        return null;
    }
}
