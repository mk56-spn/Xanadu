using Xanadu.Systems.Audio;

namespace Xanadu.Tests.Support;

/// <summary>Configurable recording audio backend shared by transport tests.</summary>
public sealed class RecordingTrackAudio : ITrackAudio
{
    public int LoadCount { get; private set; }
    public int PlayCount { get; private set; }
    public int ResumeCount { get; private set; }
    public int PauseCount { get; private set; }
    public int StopCount { get; private set; }
    public int SeekCount { get; private set; }

    public string? LoadedPath { get; private set; }
    public float Volume { get; private set; }
    public float SeekSeconds { get; private set; }
    public float PositionSeconds { get; set; }
    public float LengthSeconds { get; set; }
    public bool IsPlaying { get; private set; }
    public bool Stopped { get; private set; }
    public bool LoadSucceeds { get; set; } = true;
    public bool ResumeSucceeds { get; set; } = true;

    public bool Play(string audioPath, float volume)
    {
        PlayCount++;
        Stopped = false;
        return Load(audioPath, volume) && Resume();
    }

    public bool Load(string audioPath, float volume)
    {
        LoadCount++;
        LoadedPath = audioPath;
        Volume = volume;
        return LoadSucceeds;
    }

    public bool Resume()
    {
        ResumeCount++;
        IsPlaying = ResumeSucceeds;
        return ResumeSucceeds;
    }

    public void Pause()
    {
        PauseCount++;
        IsPlaying = false;
    }

    public void Seek(float seconds)
    {
        SeekCount++;
        SeekSeconds = seconds;
        PositionSeconds = seconds;
    }

    public void SetVolume(float volume) => Volume = volume;

    public void Stop()
    {
        StopCount++;
        Stopped = true;
        IsPlaying = false;
    }

    public void Dispose() { }
}
