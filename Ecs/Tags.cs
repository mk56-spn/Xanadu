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

/// <summary>Marks a play note that has already passed its timing window.</summary>
public struct NoteMissed : ITag { }

/// <summary>Marks a playfield entity whose audio is paused by the pause screen.</summary>
public struct PlayPaused : ITag { }

/// <summary>Marks an entity whose momentum should be applied to its physics body.</summary>
public struct ApplyMomentum : ITag { }
