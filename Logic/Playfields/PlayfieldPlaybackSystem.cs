using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Audio;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Logic.Playfields;

/// <summary>Loads the active playfield song and drives its playhead from miniaudio.</summary>
public sealed class PlayfieldPlaybackSystem(ITrackAudio audio) : QuerySystem<LevelInfo, Playhead>
{
    /// <summary>How quickly the smooth clock eases to the audio clock, per second.</summary>
    private const double ClockPull = 4d;

    /// <summary>The smooth clock is put straight on the audio clock when it is this far off, ms (a seek, a stall).</summary>
    private const double ClockSnapMs = 80d;

    private readonly ITrackAudio _audio = audio;
    private int _loadedRevision = -1;
    private int _loadedEntityId = -1;

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

        double clockMs = _audio.PositionSeconds * 1000d;
        playhead.ClockTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        playhead.ClockMs = playhead.DurationMs > 0 ? Math.Min(clockMs, playhead.DurationMs) : clockMs;
        playhead.TimeMs = (long)playhead.ClockMs;

        if (playhead.DurationMs > 0 && playhead.TimeMs >= playhead.DurationMs)
            End(ref playhead);
    }

    /// <summary>
    /// Steps the smooth clock: frame time while playing, pulled toward the
    /// audio clock and put on it when too far off; held, it is the audio clock.
    /// </summary>
    private void Smooth(ref Playhead playhead)
    {
        if (!playhead.Playing)
        {
            playhead.ClockTicks = 0;
            playhead.SmoothMs = playhead.TimeMs;
            return;
        }

        double delta = Tick.deltaTime;
        double clock = playhead.SmoothMs + delta * 1000d;
        double drift = playhead.TimeMs - clock;
        playhead.SmoothMs = Math.Abs(drift) > ClockSnapMs
            ? playhead.TimeMs
            : clock + drift * (1d - Math.Exp(-ClockPull * delta));
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

            System.Smooth(ref playhead);
        }
    }
}

