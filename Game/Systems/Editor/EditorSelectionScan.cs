using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>Picks selected items by a rule; the outline and grass panels both scan the selection this way.</summary>
internal static class EditorSelectionScan
{
    /// <summary>The first selected item the rule accepts, or default when none does.</summary>
    public static Entity First(NoteSelection selection, Func<Entity, bool> match)
    {
        foreach (Entity item in selection.AllNotes)
        {
            if (match(item))
                return item;
        }

        return default;
    }

    /// <summary>Replaces a target list with the selected items the rule accepts.</summary>
    public static void Collect(NoteSelection selection, List<Entity> into, Func<Entity, bool> match)
    {
        into.Clear();
        foreach (Entity item in selection.AllNotes)
        {
            if (match(item))
                into.Add(item);
        }
    }
}
