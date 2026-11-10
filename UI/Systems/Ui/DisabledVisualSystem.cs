using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Ui;

/// <summary>Dims greyed-out buttons; <see cref="ButtonVisualSystem"/> styles the rest.</summary>
public sealed class DisabledVisualSystem : QuerySystem<ButtonVisual, FillColor>
{
    public DisabledVisualSystem() => Filter.AllTags(Tags.Get<UiDisabled>());

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        Query.ForEachEntity((ref ButtonVisual visual, ref FillColor fill, Entity entity) =>
        {
            Color target = visual.Normal.Darkened(0.55f);
            if (fill.Value == target)
                return;

            fill.Value = target;
            buffer.AddTag<Dirty>(entity.Id);
        });
    }
}
