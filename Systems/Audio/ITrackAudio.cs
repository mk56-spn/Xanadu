using System;

namespace Writer.Systems.Audio;

/// <summary>Audio backend used by the track playback system.</summary>
public interface ITrackAudio : IDisposable
{
    bool Play(string audioPath, float volume);

    void Stop();

    bool IsPlaying { get; }

    float PositionSeconds { get; }

    float LengthSeconds { get; }
}
