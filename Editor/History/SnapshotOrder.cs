using Godot;

namespace Xanadu.Editor;

/// <summary>
/// The orders undo snapshots are stored in: by time, then across the field.
/// Two runs capturing the same level must produce equal lists, so every kind
/// sorts on the same stable keys.
/// </summary>
public static class SnapshotOrder
{
    public static int ByTimeThenPosition(long timeA, Vector2 a, long timeB, Vector2 b)
    {
        int time = timeA.CompareTo(timeB);
        if (time != 0)
            return time;

        return ByPosition(a, b);
    }

    public static int ByPosition(Vector2 a, Vector2 b)
    {
        int x = a.X.CompareTo(b.X);
        return x != 0 ? x : a.Y.CompareTo(b.Y);
    }
}
