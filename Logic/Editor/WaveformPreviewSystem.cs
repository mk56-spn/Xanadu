using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Ecs.Components;
using Xanadu.Logic.Audio;

namespace Xanadu.Logic.Editor;

/// <summary>Loads a cached waveform once when the editor song changes.</summary>
public sealed class WaveformPreviewSystem : QuerySystem<LevelInfo, Playhead, WaveformPreview>
{
    protected override void OnUpdate() => Query.Each(new LoadWaveform());

    private readonly record struct LoadWaveform : IEach<LevelInfo, Playhead, WaveformPreview>
    {
        public void Execute(ref LevelInfo level, ref Playhead playhead, ref WaveformPreview preview)
        {
            if (preview.Path == level.AudioPath)
                return;

            preview.Path = level.AudioPath;
            preview.Data = WaveformLoader.Load(level.AudioPath);

            if (playhead.DurationMs <= 0 && preview.Data is { DurationMs: > 0 } data)
            {
                playhead.DurationMs = data.DurationMs;
                playhead.SeekRequestMs = 0;
            }
        }
    }
}
