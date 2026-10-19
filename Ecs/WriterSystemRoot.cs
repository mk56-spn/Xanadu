using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Systems;

namespace Writer.Ecs;

/// <summary>
/// Central system registration point. Main owns only Godot lifecycle;
/// systems are added here instead of polluting Main.
/// </summary>
public sealed class WriterSystemRoot : SystemRoot
{
    public WriterSystemRoot(EntityStore entities, Rid parentCanvas) : base(entities, "Writer")
    {
        var canvas = new GodotRectCanvas();
        Add(new LayerParentSystem(parentCanvas));
        Add(new DrawableInitialiseSystem());
        Add(new RectDrawSystem(canvas));
        Add(new TextDrawSystem(canvas));
    }
}
