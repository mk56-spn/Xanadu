namespace Xanadu.Systems.Debug;

/// <summary>Provides current frame rate to the debug overlay.</summary>
public interface IFpsSource
{
    double GetFps();
}
