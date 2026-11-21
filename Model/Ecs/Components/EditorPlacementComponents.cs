using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Settings;

namespace Xanadu.Model.Ecs.Components;

/// <summary>What clicking the field places.</summary>
public enum EditorPlacementMode
{
    /// <summary>A plain action note, where clicked, at the snapped playhead.</summary>
    Note,
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
