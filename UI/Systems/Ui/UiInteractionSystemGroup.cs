using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Systems;

namespace Xanadu.Systems.Ui;

/// <summary>Generic pointer, focus, toggle, dropdown, wheel, button and tooltip interaction.</summary>
public sealed class UiInteractionSystemGroup : SystemGroup
{
    public UiInteractionSystemGroup(
        EntityStore entities,
        IPointerInputSource pointer,
        ScrollSystem scroll,
        IKeyboardState? keyboard = null,
        IVisualRidFactory? visuals = null,
        Vector2 screen = default,
        bool focus = true,
        bool tooltips = true) : base("UiInteraction")
    {
        visuals ??= HostPlatform.Visuals;
        keyboard ??= HostPlatform.Keyboard;

        Add(new InputSystem(pointer));
        if (focus)
            Add(new UiFocusSystem(entities, pointer, keyboard, visuals, screen));
        Add(new UiWheelSystem(entities, pointer));
        Add(new UiDropdownSystem(entities));
        Add(new UiTreeSystem());
        Add(new UiCollapsibleSystem());
        Add(new TextFieldFocusSystem());
        Add(new TextFieldDoubleClickSystem());
        Add(new TextFieldSystem());
        Add(new DisabledTextFieldSystem());
        Add(scroll);
        Add(new ToggleSystem());
        Add(new UiTabSystem());
        Add(new ButtonVisualSystem());
        Add(new DisabledVisualSystem());
        Add(new UiSliderSystem(pointer));
        Add(new UiColorPickerSystem(pointer));
        Add(new UiColorDragSystem(entities, pointer, visuals));
        Add(new UiColorFieldSystem());
        if (tooltips)
            Add(new UiTooltipSystem(entities, pointer, visuals, screen));
    }
}
