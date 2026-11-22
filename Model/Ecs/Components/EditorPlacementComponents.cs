using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Settings;

namespace Xanadu.Model.Ecs.Components;

/// <summary>What clicking the field places.</summary>
public enum EditorPlacementMode
{
    /// <summary>A plain action note at the snapped playhead; it lands on its key's line of the chord then.</summary>
    Note,

    /// <summary>A chord, its first line meeting the judgement line where clicked, from the snapped playhead.</summary>
    Chord,

    /// <summary>A dash note at the snapped playhead: a bar across its chord, hit with any direction key.</summary>
    Dash,

    /// <summary>The level's entrance: a rift where clicked, releasing the player at the snapped playhead.</summary>
    Entrance,

    /// <summary>
    /// An orb hold: a click places one at the snapped playhead, two beats long;
    /// with it still selected, clicks further on add height keys, stretching its tail.
    /// </summary>
    Orb,

    /// <summary>A pad, centred where clicked; untimed, it fires on touch.</summary>
    Pad,

    /// <summary>A timed pad, centred where clicked, firing at the snapped playhead.</summary>
    TimedPad,

    /// <summary>
    /// An updraft emitter, centred where clicked; with it still selected, a
    /// click above it in its column sets its hover height there.
    /// </summary>
    Draft,

    /// <summary>A dash ender, centred where clicked: a dash entering it ends there.</summary>
    DashStop,
}

/// <summary>The editor's placement brush: what clicking the field places, and what a new note gets.</summary>
public struct EditorPlacement() : IComponent
{
    public EditorPlacementMode Mode = EditorPlacementMode.Note;

    /// <summary>The action key new notes get.</summary>
    public KeybindAction Note = KeybindAction.Action1;
}

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
