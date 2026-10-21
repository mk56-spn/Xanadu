using Friflo.Engine.ECS;

namespace Writer.Ecs;

/// <summary>Playback state for a miniaudio-backed test track.</summary>
public struct TrackProgress(
    string audioPath,
    long durationMs,
    float bpm,
    long offsetMs = 0,
    int speedPercent = 100) : IComponent
{
    public string AudioPath = audioPath;
    public long DurationMs = durationMs;
    public float Bpm = bpm;
    public long OffsetMs = offsetMs;
    public int SpeedPercent = speedPercent;
    public long NowMs;
    public long AudioMs;
    public bool Started;
    public bool Finished;
}
