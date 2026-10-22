using Friflo.Engine.ECS;
using Writer.Settings;

namespace Writer.Ecs;

/// <summary>Settings button that captures a new key for an action.</summary>
public struct KeybindButton(KeybindAction action) : IComponent
{
    public KeybindAction Action = action;
}

/// <summary>Settings button that toggles vertical sync.</summary>
public struct VSyncToggle : IComponent;
