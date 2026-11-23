using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Audio;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Logic.Audio;

/// <summary>Advances Beatoraja-style TIMER_PLAY and keeps miniaudio in sync.</summary>
public sealed class TrackPlaybackSystem(ITrackAudio audio, ITrackClock clock) : QuerySystem<TrackProgress>
{
    private readonly ITrackAudio _audio = audio;
    private readonly ITrackClock _clock = clock;
    private readonly TrackTimer _timer = new();

    protected override void OnUpdate()
    {
        long now = _clock.NowMicros;

        Query.Each(new Advance(_audio, _timer, now));
    }

    private readonly record struct Advance(ITrackAudio Audio, TrackTimer Timer, long Now) : IEach<TrackProgress>
    {
        public void Execute(ref TrackProgress progress)
        {
            if (!progress.Started)
            {
                progress.Started = true;
                Timer.Start(Now, progress.OffsetMs * 1000);
                Audio.Play(progress.AudioPath, 0.8f);

                float length = Audio.LengthSeconds;
                if (length > 0)
                    progress.DurationMs = (long)(length * 1000);
            }

            if (progress.Finished)
                return;

            Timer.Update(Now, progress.SpeedPercent);
            progress.NowMs = Math.Min(Timer.NowMs, progress.DurationMs);
            progress.AudioMs = (long)(Audio.PositionSeconds * 1000);

            if (progress.NowMs < progress.DurationMs)
                return;

            progress.NowMs = progress.DurationMs;
            progress.Finished = true;
            Audio.Stop();
        }
    }
}
