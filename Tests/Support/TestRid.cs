using System.Reflection;
using Godot;

namespace Xanadu.Tests.Support;

/// <summary>Distinct canvas ids for tests, which have no rendering server to hand them out.</summary>
public static class TestRid
{
    private static readonly FieldInfo Id = typeof(Rid).GetField(
        "_id",
        BindingFlags.NonPublic | BindingFlags.Instance)!;

    public static Rid Make(ulong id)
    {
        object boxed = new Rid();
        Id.SetValue(boxed, id);
        return (Rid)boxed;
    }
}
