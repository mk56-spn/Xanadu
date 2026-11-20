namespace Xanadu.Logic.Audio;

/// <summary>Monotonic microsecond clock used by the track timer.</summary>
public interface ITrackClock
{
    long NowMicros { get; }
}
