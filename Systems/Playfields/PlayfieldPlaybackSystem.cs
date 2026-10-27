using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Audio;

namespace Xanadu.Systems.Playfields;

/// <summary>Loads the active playfield song and drives its playhead from miniaudio.</summary>
public sealed class PlayfieldPlaybackSystem : QuerySystem<LevelInfo, Playhead>
{
    private readonly ITrackAudio _audio;
    private int _loadedRevision = -1;
    private int _loadedEntityId = -1;

    public PlayfieldPlaybackSystem(ITrackAudio audio)
    {
        _audio = audio;
    }

    protected override void OnUpdate()
    {
        if (Query.Count == 0)
        {
            if (_audio.IsPlaying)
                _audio.Stop();

            _loadedRevision = -1;
            _loadedEntityId = -1;
            return;
        }

        Query.ForEachEntity((ref level, ref playhead, entity) =>
        {
            if (playhead.AudioRevision != _loadedRevision ||
                entity.Id != _loadedEntityId)
            {
                Load(level, ref playhead);
            }

            _loadedEntityId = entity.Id;

            if (playhead.SeekRequestMs >= 0)
                ApplySeek(ref playhead);

            if (playhead.Playing)
                UpdatePlaying(ref playhead);
            else if (_audio.IsPlaying)
                _audio.Pause();
        });
    }

    private void UpdatePlaying(ref Playhead playhead)
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

    private void End(ref Playhead playhead)
    {
        if (playhead.DurationMs > 0)
            playhead.TimeMs = playhead.DurationMs;

        playhead.Playing = false;
        _audio.Pause();
    }

    private void Load(LevelInfo level, ref Playhead playhead)
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

    private void ApplySeek(ref Playhead playhead)
    {
        long seek = playhead.SeekRequestMs;
        if (playhead.DurationMs > 0)
            seek = Math.Clamp(seek, 0, playhead.DurationMs);

        _audio.Seek(seek / 1000f);
        playhead.TimeMs = seek;
        playhead.SeekRequestMs = -1;
    }
}
