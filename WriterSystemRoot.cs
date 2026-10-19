using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Writer;

/// <summary>
/// Central system registration point. Main owns only Godot lifecycle;
/// systems are added here instead of polluting Main.
/// </summary>
public sealed class WriterSystemRoot(EntityStore entities) : SystemRoot(entities, "Writer")
{
    // Add systems here.
}
