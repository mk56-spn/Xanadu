using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Settings;

namespace Xanadu.Model.Ecs.Components;

/// <summary>What clicking the field places.</summary>
public enum EditorPlacementMode
{
    /// <summary>A route note: the head where first clicked at the playhead, then a turn at the playhead with each click after, until Enter.</summary>
    Curve,

    /// <summary>A dash note: pressed where it goes, dragged out to how far it teleports.</summary>
    Dash,

    /// <summary>A hold note: the head where first clicked at the playhead, then the tail where clicked next, at the playhead then.</summary>
    Hold,
}

/// <summary>The editor's placement brush: what clicking the field places, and what a new note gets.</summary>
public struct EditorPlacement() : IComponent
{
    public EditorPlacementMode Mode = EditorPlacementMode.Curve;

    /// <summary>The action key new notes get.</summary>
    public KeybindAction Note = KeybindAction.Action1;

    /// <summary>The 1×/2×/3× speed new curve points get.</summary>
    public DashStrength Strength = DashStrength.Single;
}

/// <summary>
/// A dash note being dragged out, on the editor entity: the head's place
/// and time from the press, and where the pointer is now.
/// </summary>
public struct EditorDragDraft : IComponent
{
    public bool Active;
    public EditorPlacementMode Mode;
    public Vector2 Head;
    public Vector2 Current;
    public long StartMs;
}

/// <summary>The hold length readout in the tool panel, which shows the selected hold's length.</summary>
public struct EditorHoldLengthReadout : IComponent;

/// <summary>A part of the left panel that only shows for the tools that use it.</summary>
public struct EditorToolSection(EditorPlacementMode[] modes) : IComponent
{
    public EditorPlacementMode[] Modes = modes;
}

/// <summary>Where the pointer would place the current tool's item this frame, if anywhere.</summary>
public struct EditorPlacementGhost : IComponent
{
    public bool Visible;
    public Vector2 Screen;
}
