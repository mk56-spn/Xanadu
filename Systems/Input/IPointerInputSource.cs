namespace Writer.Systems.Input;

/// <summary>Provides the current pointer state to input systems.</summary>
public interface IPointerInputSource
{
    PointerInput Read();
}
