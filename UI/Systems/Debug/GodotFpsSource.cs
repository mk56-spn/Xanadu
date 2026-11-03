using Godot;

namespace Xanadu.Systems.Debug;

/// <summary>Reads the current frame rate from Godot.</summary>
public sealed class GodotFpsSource : IFpsSource
{
    public double GetFps() => Engine.GetFramesPerSecond();
}
