using Friflo.Engine.ECS;

namespace Writer;

/// <summary>Marks an entity whose visual state changed and needs a redraw.</summary>
public struct Dirty : ITag { }
