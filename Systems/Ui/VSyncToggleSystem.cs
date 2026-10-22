using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;
using Writer.Settings;

namespace Writer.Systems.Ui;

/// <summary>Applies ECS VSync toggle changes to display settings.</summary>
public sealed class VSyncToggleSystem : QuerySystem<Toggle>
{
    private readonly DisplaySettings _display;

    public VSyncToggleSystem(DisplaySettings display)
    {
        _display = display;
        Filter.AllTags(Tags.Get<ToggleChanged>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref toggle, entity) =>
        {
            if (entity.HasComponent<VSyncToggle>())
                _display.SetVSync(toggle.Value);

            buffer.RemoveTag<ToggleChanged>(entity.Id);
        });
    }
}
