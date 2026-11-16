using System;
using Friflo.Engine.ECS.Systems;

namespace Xanadu.Labs.Experiments.EcsRender;

internal sealed class FrameCounterSystem : QuerySystem<FrameCounter, Label>
{
    protected override void OnUpdate()
    {
        foreach (var (counters, labels, entities) in Query.Chunks)
        {
            Span<FrameCounter> counter = counters.Span;
            Span<Label> label = labels.Span;
            for (int i = 0; i < entities.Length; i++)
                label[i].Text = $"frame {++counter[i].Frames:N0}";
        }
    }
}
