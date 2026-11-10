using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>A rectangle filled with a linear colour gradient.</summary>
public struct LinearGradient(Color start, Color end, bool horizontal = true) : IComponent
{
    public Color Start = start;
    public Color End = end;
    public bool Horizontal = horizontal;
}
