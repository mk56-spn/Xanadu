using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;
using Writer.Systems.Audio;

namespace Writer.Systems.Editor;

/// <summary>Loads the editor song and drives the playhead from miniaudio.</summary>
public sealed class EditorPlaybackSystem : QuerySystem<LevelInfo, EditorPlayhead>
{
    private readonly ITrackAudio _audio;
    private string? _loadedPath;

    public EditorPlaybackSystem(ITrackAudio audio)
    {
        _audio = audio;
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref level, ref playhead, _) =>
        {
            if (level.AudioPath != _loadedPath)
                Load(level, ref playhead);

            if (playhead.SeekRequestMs >= 0)
                ApplySeek(ref playhead);

            if (playhead.Playing)
            {
                if (!_audio.IsPlaying)
                    _audio.Resume();

                if (playhead.DurationMs > 0)
                {
                    playhead.TimeMs = Math.Min(
                        (long)(_audio.PositionSeconds * 1000),
                        playhead.DurationMs);
                }
                else
                {
                    playhead.TimeMs = (long)(_audio.PositionSeconds * 1000);
                }

                if (playhead.DurationMs > 0 && playhead.TimeMs >= playhead.DurationMs)
                {
                    playhead.TimeMs = playhead.DurationMs;
                    playhead.Playing = false;
                    _audio.Pause();
                }
            }
            else if (_audio.IsPlaying)
            {
                _audio.Pause();
            }
        });
    }

    private void Load(LevelInfo level, ref EditorPlayhead playhead)
    {
        _loadedPath = level.AudioPath;
        playhead.Playing = false;
        playhead.TimeMs = 0;
        playhead.DurationMs = 0;
        playhead.SeekRequestMs = -1;

        if (string.IsNullOrEmpty(level.AudioPath))
        {
            _audio.Stop();
            return;
        }

        if (_audio.Load(level.AudioPath, 0.8f))
            playhead.DurationMs = (long)(_audio.LengthSeconds * 1000);
    }

    private void ApplySeek(ref EditorPlayhead playhead)
    {
        long seek = playhead.SeekRequestMs;
        if (playhead.DurationMs > 0)
            seek = Math.Clamp(seek, 0, playhead.DurationMs);

        _audio.Seek(seek / 1000f);
        playhead.TimeMs = seek;
        playhead.SeekRequestMs = -1;
    }
}
