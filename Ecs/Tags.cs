using Friflo.Engine.ECS;

namespace Writer.Ecs;

/// <summary>Marks an entity whose visual state changed and needs a redraw.</summary>
public struct Dirty : ITag { }

/// <summary>Marks a layer that still needs its canvas item parented.</summary>
public struct Uninitialised : ITag { }

/// <summary>Marks a drawable that participates in pointer hit testing.</summary>
public struct TakesInput : ITag { }

/// <summary>Marks the drawable currently holding the pointer button.</summary>
public struct Pressed : ITag { }

/// <summary>Marks a container whose child layout needs recomputing.</summary>
public struct LayoutDirty : ITag { }

/// <summary>Marks a drawable whose canvas transform needs updating.</summary>
public struct Moved : ITag { }
