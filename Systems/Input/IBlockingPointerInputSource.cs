namespace Writer.Systems.Input;

/// <summary>Pointer source whose input can be temporarily suppressed by a modal UI.</summary>
public interface IBlockingPointerInputSource : IPointerInputSource
{
    bool Blocked { get; }

    void SetBlocked(bool blocked);
}
