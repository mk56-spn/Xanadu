using Friflo.Engine.ECS;

namespace Writer.Ecs;

/// <summary>Transient timed notification shown by the general toast system.</summary>
public struct Toast : IComponent
{
    public string Message;
    public float Age;
    public float Duration;
    public bool Active;
}
