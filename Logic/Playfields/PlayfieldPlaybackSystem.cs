using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Ecs.Components;
using Xanadu.Audio;

namespace Xanadu.Logic.Playfields;

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

        Query.EachEntity(new Play(this));
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
        // A seek asked for before the audio loaded still stands: returning from a
        // preview puts the playhead back before this first load of the new editor.
        _loadedRevision = playhead.AudioRevision;
        playhead.Playing = false;
        playhead.TimeMs = 0;
        playhead.DurationMs = 0;

        if (level.Song is not { } song)
        {
            _audio.Stop();
            return;
        }

        if (_audio.Load(song.AudioFile, 0.8f))
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

    private readonly record struct Play(PlayfieldPlaybackSystem System) : IEachEntity<LevelInfo, Playhead>
    {
        public void Execute(ref LevelInfo level, ref Playhead playhead, int id)
        {
            if (playhead.AudioRevision != System._loadedRevision ||
                id != System._loadedEntityId)
            {
                System.Load(level, ref playhead);
            }

            System._loadedEntityId = id;

            if (playhead.SeekRequestMs >= 0)
                System.ApplySeek(ref playhead);

            if (playhead.Playing)
                System.UpdatePlaying(ref playhead);
            else if (System._audio.IsPlaying)
                System._audio.Pause();
        }
    }
}
