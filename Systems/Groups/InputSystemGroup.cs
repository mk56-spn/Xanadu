using Friflo.Engine.ECS.Systems;
using Writer.Systems.Input;

namespace Writer.Systems.Groups;

/// <summary>Resolves pointer input against the ECS drawables.</summary>
public sealed class InputSystemGroup : SystemGroup
{
    public InputSystemGroup(IPointerInputSource pointerInput) : base("Input")
    {
        Add(new InputSystem(pointerInput));
    }
}
