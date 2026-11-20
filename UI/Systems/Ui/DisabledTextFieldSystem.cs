using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>Paints greyed-out text fields with the flat disabled colour.</summary>
public sealed class DisabledTextFieldSystem : QuerySystem<TextField, FillColor>
{
    public DisabledTextFieldSystem() => Filter.AllTags(Tags.Get<UiDisabled>());

    protected override void OnUpdate()
    {
        Query.EachEntity(new GreyDisabled(CommandBuffer));
    }

    private readonly record struct GreyDisabled(CommandBuffer Buffer) : IEachEntity<TextField, FillColor>
    {
        public void Execute(ref TextField _, ref FillColor fill, int id)
        {
            if (fill.Value == UiTheme.Disabled)
                return;

            fill.Value = UiTheme.Disabled;
            Buffer.AddTag<Dirty>(id);
        }
    }
}
