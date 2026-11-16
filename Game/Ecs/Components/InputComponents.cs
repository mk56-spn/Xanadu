using Friflo.Engine.ECS;

namespace Xanadu.Ecs.Components;

/// <summary>Settings button that cycles the window mode: windowed, fullscreen, borderless.</summary>
public struct WindowModeButton : IComponent
{
    public bool Held;
}
