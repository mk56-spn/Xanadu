using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Systems.Scenes;

namespace Xanadu.Systems.Groups;

/// <summary>Resolves pointer input against ECS drawables and scene buttons.</summary>
public sealed class InputSystemGroup : SystemGroup
{
    public InputSystemGroup(
        EntityStore entities,
        IPointerInputSource pointerInput,
        SceneSystem scenes,
        KeybindSettings keybinds,
        DisplaySettings display,
        ScrollSystem scroll,
        IKeyboardState? keyboard = null,
        IVisualRidFactory? visuals = null,
        Vector2 screen = default)
        : base("Input")
    {
        Add(new UiInteractionSystemGroup(
            entities,
            pointerInput,
            scroll,
            keyboard,
            visuals,
            screen));
        Add(new VSyncToggleSystem(display));
        Add(scenes);
        Add(new KeybindUiSystem(keybinds));
    }
}
