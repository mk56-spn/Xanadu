using System;

namespace Writer.Systems.Audio;

/// <summary>Audio backend used by track and editor transport systems.</summary>
public interface ITrackAudio : IDisposable
{
    bool Play(string audioPath, float volume);

    bool Load(string audioPath, float volume);

    bool Resume();

    void Pause();

    void Seek(float seconds);

    void Stop();

    bool IsPlaying { get; }

    float PositionSeconds { get; }

    float LengthSeconds { get; }
}
