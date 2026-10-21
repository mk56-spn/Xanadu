using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Systems.Editor;
using Writer.Systems.Input;
using Writer.Systems.Scenes;

namespace Writer.Systems.Groups;

/// <summary>Resolves pointer input against ECS drawables, warnings, and scene buttons.</summary>
public sealed class InputSystemGroup : SystemGroup
{
    public InputSystemGroup(
        EntityStore entities,
        IPointerInputSource pointerInput,
        SceneSystem scenes)
        : base("Input")
    {
        Add(new InputSystem(pointerInput));
        Add(new WarningPopupSystem(entities));
        Add(scenes);
    }
}
