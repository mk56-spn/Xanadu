using System.Collections.Generic;

namespace Xanadu.Model.Levels;

/// <summary>
/// Placed items' ids (their Xif ids), which groups hold them by: every deco
/// gets one, and no two share one. Filled when a level is opened, and for a
/// new item when it is placed.
/// </summary>
public static class LevelItemIds
{
    /// <summary>
    /// Gives every deco with no id, or a taken one, a fresh id, and lets the
    /// groups go of ids no deco has (an item deleted some way that didn't tell them).
    /// </summary>
    public static void Fill(LevelDocument level)
    {
        var taken = new HashSet<string>();
        foreach (DecoDocument deco in level.Decos)
        {
            if (deco.Xif.Id.Length == 0 || !taken.Add(deco.Xif.Id))
            {
                deco.Xif.Id = Fresh(taken, deco.Xif.Name);
                taken.Add(deco.Xif.Id);
            }
        }

        foreach (GroupDocument group in level.Groups)
            group.KeepOnly(taken);
    }

    /// <summary>The first of <c>name-1</c>, <c>name-2</c>… not in <paramref name="taken"/>.</summary>
    public static string Fresh(IReadOnlySet<string> taken, string name)
    {
        string stem = name.Length > 0 ? name : "item";
        for (int n = 1; ; n++)
        {
            string id = $"{stem}-{n}";
            if (!taken.Contains(id))
                return id;
        }
    }
}
