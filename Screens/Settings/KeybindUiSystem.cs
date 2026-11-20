using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Settings;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Settings;

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
        Query.EachEntity(new ShowKeys(Query.Store, CommandBuffer, _keybinds));
    }

    private readonly record struct ShowKeys(EntityStore Store, CommandBuffer Buffer, KeybindSettings Keybinds) : IEachEntity<UiCommand>
    {
        public void Execute(ref UiCommand command, int id)
        {
            Entity entity = Store.GetEntityById(id);

            if (!command.Is(out KeybindAction action) || !entity.HasComponent<Text>())
                return;

            string value = Keybinds.Capturing == action
                ? "PRESS A KEY..."
                : KeybindSettings.KeyLabel(Keybinds.Get(action));

            ref Text text = ref entity.GetComponent<Text>();
            if (text.Value == value)
                return;

            text.Value = value;
            Buffer.AddTag<Dirty>(id);
        }
    }
}
