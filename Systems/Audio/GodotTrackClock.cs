using Godot;

namespace Writer.Systems.Audio;

/// <summary>Reads Godot's monotonic microsecond tick counter.</summary>
public sealed class GodotTrackClock : ITrackClock
{
    public long NowMicros => (long)Time.GetTicksUsec();
}
