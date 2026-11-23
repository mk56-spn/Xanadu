namespace Xanadu.Core.Items;

/// <summary>Deep copy and comparison of item trees, for snapshots that must not share them.</summary>
public static class XifCopy
{
    public static Xif Clone(Xif item)
    {
        var copy = new Xif { Id = item.Id, Name = item.Name, Owner = item.Owner, Ref = item.Ref };
        foreach (XifModule module in item.Modules)
            copy.Modules.Add(module.Copy());
        foreach (Xif child in item.Children)
            copy.Children.Add(Clone(child));
        return copy;
    }

    public static bool AreEqual(Xif a, Xif b)
    {
        if (a.Id != b.Id || a.Name != b.Name || a.Owner != b.Owner || a.Ref != b.Ref ||
            a.Modules.Count != b.Modules.Count || a.Children.Count != b.Children.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Modules.Count; i++)
        {
            if (!a.Modules[i].Same(b.Modules[i]))
                return false;
        }

        for (int i = 0; i < a.Children.Count; i++)
        {
            if (!AreEqual(a.Children[i], b.Children[i]))
                return false;
        }
        return true;
    }
}
