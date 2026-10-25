using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Systems.Input;
using Writer.Systems.Ui;

namespace Writer.Systems.Ui;

/// <summary>Generic pointer, focus, toggle, dropdown, and button interaction.</summary>
public sealed class UiInteractionSystemGroup : SystemGroup
{
    public UiInteractionSystemGroup(
        EntityStore entities,
        IPointerInputSource pointer,
        ScrollSystem scroll) : base("UiInteraction")
    {
        Add(new InputSystem(pointer));
        Add(new UiDropdownSystem(entities));
        Add(new TextFieldFocusSystem());
        Add(new TextFieldSystem());
        Add(scroll);
        Add(new ToggleSystem());
        Add(new ButtonVisualSystem());
    }
}
