using Xanadu.Core;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>Dims greyed-out buttons; <see cref="ButtonVisualSystem"/> styles the rest.</summary>
public sealed class DisabledVisualSystem : QuerySystem<ButtonVisual, FillColor>
{
    public DisabledVisualSystem() => Filter.AllTags(Tags.Get<UiDisabled>());

    protected override void OnUpdate()
    {
        Query.EachEntity(new DarkenDisabled(CommandBuffer));
    }

    private readonly record struct DarkenDisabled(CommandBuffer Buffer) : IEachEntity<ButtonVisual, FillColor>
    {
        public void Execute(ref ButtonVisual visual, ref FillColor fill, int id)
        {
            Color target = visual.Normal.Darkened(0.55f);
            if (fill.Value == target)
                return;

            fill.Value = target;
            Buffer.AddTag<Dirty>(id);
        }
    }
}
