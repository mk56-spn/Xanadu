using Xanadu.Systems.Input;

namespace Xanadu.Tests.Support;

/// <summary>Pointer source whose sample the test sets before each frame.</summary>
public sealed class FakePointerInput : IPointerInputSource
{
    public PointerInput Current;

    public PointerInput Read() => Current;
}
