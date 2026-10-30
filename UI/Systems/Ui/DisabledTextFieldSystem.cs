using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Ui;

/// <summary>Paints greyed-out text fields with the flat disabled colour.</summary>
public sealed class DisabledTextFieldSystem : QuerySystem<TextField, FillColor>
{
    public DisabledTextFieldSystem() => Filter.AllTags(Tags.Get<UiDisabled>());

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        Query.ForEachEntity((ref TextField _, ref FillColor fill, Entity entity) =>
        {
            if (fill.Value == UiTheme.Disabled)
                return;

            fill.Value = UiTheme.Disabled;
            buffer.AddTag<Dirty>(entity.Id);
        });
    }
}
