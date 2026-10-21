using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Systems.Editor;
using Writer.Systems.Input;

namespace Writer.Systems.Groups;

/// <summary>Resolves pointer input against the ECS drawables and warning popup.</summary>
public sealed class InputSystemGroup : SystemGroup
{
    public InputSystemGroup(EntityStore entities, IPointerInputSource pointerInput)
        : base("Input")
    {
        Add(new InputSystem(pointerInput));
        Add(new WarningPopupSystem(entities));
    }
}
