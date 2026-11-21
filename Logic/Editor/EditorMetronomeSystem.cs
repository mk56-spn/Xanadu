using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Ecs.Components;
using Xanadu.Audio;

namespace Xanadu.Logic.Editor;

/// <summary>Plays a click on BPM/offset beat boundaries.</summary>
public sealed class EditorMetronomeSystem : QuerySystem<Playhead, EditorMetronome>
{
    private const string ClickPath = "Assets/Audio/metronome.wav";

    private readonly ITrackAudio _audio;
    private long _lastBeat = long.MinValue;
    private bool _loaded;

    public EditorMetronomeSystem(ITrackAudio audio)
    {
        _audio = audio;
    }

    protected override void OnUpdate() => Query.Each(new Click(this));

    private readonly record struct Click(EditorMetronomeSystem System) : IEach<Playhead, EditorMetronome>
    {
        public void Execute(ref Playhead playhead, ref EditorMetronome metronome)
        {
            if (!metronome.Enabled ||
                !playhead.Playing ||
                playhead.DurationMs <= 0)
            {
                System._lastBeat = long.MinValue;
                return;
            }

            long beat = (long)Math.Floor(playhead.Tempo.BeatAt(playhead.TimeMs));

            if (beat == System._lastBeat)
                return;

            System._lastBeat = beat;

            if (!System._loaded)
            {
                if (!System._audio.Load(ClickPath, 0.45f))
                    return;

                System._loaded = true;
            }

            System._audio.Seek(0);
            System._audio.Resume();
        }
    }
}
