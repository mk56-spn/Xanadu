using Friflo.Engine.ECS.Systems;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Xanadu.Systems.Scenes;
using Xanadu.Systems.Ui;

namespace Xanadu.Systems.Groups;

/// <summary>
/// The game's answers to this frame's UI input: scene buttons, the display
/// toggles and keybind rows. The UI's own input ran before the game's root.
/// </summary>
public sealed class InputSystemGroup : SystemGroup
{
    public InputSystemGroup(
        SceneSystem scenes,
        KeybindSettings keybinds,
        DisplaySettings display)
        : base("Input")
    {
        Add(new WindowModeButtonSystem(display));
        Add(scenes);
        Add(new KeybindUiSystem(keybinds));
    }
}
