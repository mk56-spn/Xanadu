using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Ui;

/// <summary>Paints disabled text fields with the flat disabled colour.</summary>
public sealed class DisabledTextFieldSystem : QuerySystem<TextField, FillColor>
{
    private readonly EntityStore _entities;

    public DisabledTextFieldSystem(EntityStore entities)
    {
        _entities = entities;
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        foreach (Entity entity in _entities
                     .Query<TextField, FillColor>()
                     .WithDisabled()
                     .ToEntityList())
        {
            if (!entity.Tags.Has<Disabled>())
                continue;

            ref FillColor fill = ref entity.GetComponent<FillColor>();
            if (fill.Value == UiTheme.Disabled)
                continue;

            fill.Value = UiTheme.Disabled;
            buffer.AddTag<Dirty>(entity.Id);
        }
    }
}
