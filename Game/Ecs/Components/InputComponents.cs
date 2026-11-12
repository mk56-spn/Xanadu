using Friflo.Engine.ECS;
using Xanadu.Settings;

namespace Xanadu.Ecs;

/// <summary>Settings button that cycles the window mode: windowed, fullscreen, borderless.</summary>
public struct WindowModeButton : IComponent
{
    public bool Held;
}
