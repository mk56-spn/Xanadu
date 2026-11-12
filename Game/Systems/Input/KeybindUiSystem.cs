using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Input;

/// <summary>Refreshes keybind buttons, whose command is a <see cref="KeybindAction"/>, from the settings model.</summary>
public sealed class KeybindUiSystem : QuerySystem<UiCommand>
{
    private readonly KeybindSettings _keybinds;

    public KeybindUiSystem(KeybindSettings keybinds)
    {
        _keybinds = keybinds;
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref command, entity) =>
        {
            if (!command.Is(out KeybindAction action) || !entity.HasComponent<Text>())
                return;

            string value = _keybinds.Capturing == action
                ? "PRESS A KEY..."
                : KeybindSettings.KeyLabel(_keybinds.Get(action));

            ref Text text = ref entity.GetComponent<Text>();
            if (text.Value == value)
                return;

            text.Value = value;
            buffer.AddTag<Dirty>(entity.Id);
        });
    }
}
