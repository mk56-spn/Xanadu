using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Audio;

/// <summary>Advances Beatoraja-style TIMER_PLAY and keeps miniaudio in sync.</summary>
public sealed class TrackPlaybackSystem : QuerySystem<TrackProgress>
{
    private readonly ITrackAudio _audio;
    private readonly ITrackClock _clock;
    private readonly TrackTimer _timer = new();

    public TrackPlaybackSystem(ITrackAudio audio, ITrackClock clock)
    {
        _audio = audio;
        _clock = clock;
    }

    protected override void OnUpdate()
    {
        long now = _clock.NowMicros;

        Query.ForEachEntity((ref progress, _) =>
        {
            if (!progress.Started)
            {
                progress.Started = true;
                _timer.Start(now, progress.OffsetMs * 1000);
                _audio.Play(progress.AudioPath, 0.8f);

                float length = _audio.LengthSeconds;
                if (length > 0)
                    progress.DurationMs = (long)(length * 1000);
            }

            if (progress.Finished)
                return;

            _timer.Update(now, progress.SpeedPercent);
            progress.NowMs = Math.Min(_timer.NowMs, progress.DurationMs);
            progress.AudioMs = (long)(_audio.PositionSeconds * 1000);

            if (progress.NowMs < progress.DurationMs)
                return;

            progress.NowMs = progress.DurationMs;
            progress.Finished = true;
            _audio.Stop();
        });
    }
}
