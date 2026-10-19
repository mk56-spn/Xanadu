using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Systems;

namespace Writer;

#pragma warning disable IDE0290 // The constructor configures the query filter before adding the system.
/// <summary>
/// Central system registration point. Main owns only Godot lifecycle;
/// systems are added here instead of polluting Main.
/// </summary>
public sealed class WriterSystemRoot : SystemRoot
{
    public WriterSystemRoot(EntityStore entities, Rid parentCanvas) : base(entities, "Writer")
    {
        var layerParent = new LayerParentSystem(parentCanvas);
        layerParent.Filter.AllTags(Tags.Get<Uninitialised>());
        Add(layerParent);
    }
}
#pragma warning restore IDE0290
