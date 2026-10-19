using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Systems;

namespace Writer;

/// <summary>
/// Central system registration point. Main owns only Godot lifecycle;
/// systems are added here instead of polluting Main.
/// </summary>
public sealed class WriterSystemRoot : SystemRoot
{
    public WriterSystemRoot(EntityStore entities, Rid parentCanvas) : base(entities, "Writer")
    {
        Add(new LayerParentSystem(parentCanvas));
        Add(new DrawableInitialiseSystem());
        Add(new RectDrawSystem());
    }
}
