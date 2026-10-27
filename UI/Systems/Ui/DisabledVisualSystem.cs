using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Dims disabled buttons. Normal queries skip <see cref="Disabled"/> entities,
/// so this one asks for them explicitly and leaves the enabled styling to
/// <see cref="ButtonVisualSystem"/>.
/// </summary>
public sealed class DisabledVisualSystem : QuerySystem<ButtonVisual, FillColor>
{
    private readonly EntityStore _entities;

    public DisabledVisualSystem(EntityStore entities)
    {
        _entities = entities;
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        foreach (Entity entity in _entities
                     .Query<ButtonVisual, FillColor>()
                     .WithDisabled()
                     .ToEntityList())
        {
            if (!entity.Tags.Has<Disabled>())
                continue;

            Color target = entity.GetComponent<ButtonVisual>()
                .Normal
                .Darkened(0.55f);
            ref FillColor fill = ref entity.GetComponent<FillColor>();
            if (fill.Value == target)
                continue;

            fill.Value = target;
            buffer.AddTag<Dirty>(entity.Id);
        }
    }
}
