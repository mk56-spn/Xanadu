using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;
using Writer.Settings;

namespace Writer.Systems.Input;

/// <summary>Refreshes keybind buttons from the settings model.</summary>
public sealed class KeybindUiSystem : QuerySystem<KeybindButton>
{
    private readonly KeybindSettings _keybinds;

    public KeybindUiSystem(KeybindSettings keybinds)
    {
        _keybinds = keybinds;
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref button, entity) =>
        {
            if (!entity.HasComponent<Text>())
                return;

            string value = _keybinds.Capturing == button.Action
                ? "PRESS A KEY..."
                : KeybindSettings.KeyLabel(_keybinds.Get(button.Action));

            ref Text text = ref entity.GetComponent<Text>();
            if (text.Value == value)
                return;

            text.Value = value;
            buffer.AddTag<Dirty>(entity.Id);
        });
    }
}
