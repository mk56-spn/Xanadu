using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Audio;

namespace Xanadu.Systems.Editor;

/// <summary>Loads a cached waveform once when the editor song changes.</summary>
public sealed class WaveformPreviewSystem : QuerySystem<LevelInfo, Playhead, WaveformPreview>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref level, ref playhead, ref preview, entity) =>
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
        });
    }
}
