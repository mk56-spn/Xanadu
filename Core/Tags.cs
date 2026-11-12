using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>Marks an entity whose visual state changed and needs a redraw.</summary>
public struct Dirty : ITag { }

/// <summary>Marks a layer that still needs its canvas item parented.</summary>
public struct Uninitialised : ITag { }

/// <summary>Marks a drawable that participates in pointer hit testing.</summary>
public struct TakesInput : ITag { }

/// <summary>Marks a button currently under the pointer.</summary>
public struct Hovered : ITag { }

/// <summary>Marks the drawable currently holding the pointer button.</summary>
public struct Pressed : ITag { }

/// <summary>Marks a container whose child layout needs recomputing.</summary>
public struct LayoutDirty : ITag { }

/// <summary>A layer whose <c>LayerOpacity</c> has not reached its canvas item yet.</summary>
public struct LayerOpacityDirty : ITag { }

/// <summary>Excludes a child from its container's stack while keeping it alive.</summary>
public struct LayoutIgnore : ITag { }

/// <summary>
/// Authored: this node and everything under it are not shown. Set it through
/// <c>UiLayout.SetHidden</c>; <c>UiTreeEvents</c> keeps the native
/// <see cref="Disabled"/> tag on the subtree, which takes it out of every query.
/// </summary>
public struct UiHidden : ITag { }

/// <summary>
/// Marks a debug view and everything under it, so that what the debug views report
/// leaves out the views themselves. Tag the root; children inherit it.
/// </summary>
public struct DebugUi : ITag { }

/// <summary>A greyed-out control: still drawn and laid out, but it takes no input or focus.</summary>
public struct UiDisabled : ITag { }

/// <summary>Marks a canvas item whose visibility must follow its entity's enabled state.</summary>
public struct VisibilityDirty : ITag { }

/// <summary>Marks a drawable whose canvas transform needs updating.</summary>
public struct Moved : ITag { }

/// <summary>Marks a play note that has already passed its timing window.</summary>
public struct NoteMissed : ITag { }

/// <summary>Marks a playfield entity whose audio is paused by the pause screen.</summary>
public struct PlayPaused : ITag { }

