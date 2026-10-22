using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;
using Writer.Systems.Audio;

namespace Writer.Systems.Editor;

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
            WaveformData? data = WaveformLoader.Load(level.AudioPath);
            preview.Left = data?.Left ?? Array.Empty<float>();
            preview.Right = data?.Right ?? Array.Empty<float>();
            preview.DurationMs = data?.DurationMs ?? 0;

            if (playhead.DurationMs <= 0 && preview.DurationMs > 0)
            {
                playhead.DurationMs = preview.DurationMs;
                playhead.SeekRequestMs = 0;
            }
        });
    }
}
