using Godot;

namespace Xanadu.Tests.Support;

/// <summary>Distinct canvas ids for tests, which have no canvas server handing them out.</summary>
public static class TestRid
{
    public static Rid Make(ulong id) => new(id);
}
