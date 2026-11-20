using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Settings;
using Xanadu.Screens.Settings;
using Xanadu.Wiring.Scenes;

namespace Xanadu.Wiring.Groups;

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
