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
    private int _loadedRevision = -1;

    public EditorPlaybackSystem(ITrackAudio audio)
    {
        _audio = audio;
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref level, ref playhead, _) =>
        {
            if (playhead.AudioRevision != _loadedRevision)
                Load(level, ref playhead);

            if (playhead.SeekRequestMs >= 0)
                ApplySeek(ref playhead);

            if (playhead.Playing)
                UpdatePlaying(ref playhead);
            else if (_audio.IsPlaying)
                _audio.Pause();
        });
    }

    private void UpdatePlaying(ref EditorPlayhead playhead)
    {
        if (playhead.DurationMs > 0 && playhead.TimeMs >= playhead.DurationMs)
        {
            End(ref playhead);
            return;
        }

        if (!_audio.IsPlaying)
        {
            if (playhead.DurationMs > 0 &&
                _audio.PositionSeconds * 1000 >= playhead.DurationMs - 50)
            {
                End(ref playhead);
                return;
            }

            _audio.Resume();
        }

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
            End(ref playhead);
    }

    private void End(ref EditorPlayhead playhead)
    {
        if (playhead.DurationMs > 0)
            playhead.TimeMs = playhead.DurationMs;

        playhead.Playing = false;
        _audio.Pause();
    }

    private void Load(LevelInfo level, ref EditorPlayhead playhead)
    {
        _loadedRevision = playhead.AudioRevision;
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
        {
            playhead.DurationMs = (long)(_audio.LengthSeconds * 1000);
        }
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
