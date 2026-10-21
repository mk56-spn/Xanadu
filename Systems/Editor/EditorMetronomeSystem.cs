using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;
using Writer.Systems.Audio;

namespace Writer.Systems.Editor;

/// <summary>Plays a click on BPM/offset beat boundaries.</summary>
public sealed class EditorMetronomeSystem : QuerySystem<Playhead, EditorMetronome>
{
    private const string ClickPath = "res://Audio/metronome.wav";

    private readonly ITrackAudio _audio;
    private long _lastBeat = long.MinValue;
    private bool _loaded;

    public EditorMetronomeSystem(ITrackAudio audio)
    {
        _audio = audio;
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref playhead, ref metronome, _) =>
        {
            if (!metronome.Enabled ||
                !playhead.Playing ||
                playhead.DurationMs <= 0)
            {
                _lastBeat = long.MinValue;
                return;
            }

            double beatMs = 60_000.0 / Math.Max(1, playhead.Bpm);
            long beat = (long)Math.Floor(
                (playhead.TimeMs - playhead.OffsetMs) / beatMs);

            if (beat == _lastBeat)
                return;

            _lastBeat = beat;

            if (!_loaded)
            {
                if (!_audio.Load(ClickPath, 0.45f))
                    return;

                _loaded = true;
            }

            _audio.Seek(0);
            _audio.Resume();
        });
    }
}
