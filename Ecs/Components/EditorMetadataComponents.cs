using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Settings;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Ui;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

public enum EditorFieldKind
{
    LevelName,
    Artist,
    Difficulty,
    StartTime,
    StartMomentumX,
    StartMomentumY,
}

/// <summary>Binds an ECS text field to an editor metadata property.</summary>
public struct EditorField(EditorFieldKind kind) : IComponent
{
    public EditorFieldKind Kind = kind;
}

/// <summary>ECS editor HUD text entities updated from editor state.</summary>
public struct EditorHud(
    Entity level,
    Entity song,
    ShellStatusLabels status,
    Entity time,
    Entity notes) : IComponent
{
    public Entity Level = level;
    public Entity Song = song;

    /// <summary>The header's unsaved flag, shared with the animator.</summary>
    public ShellStatusLabels Status = status;
    public Entity Time = time;
    public Entity Notes = notes;

    /// <summary>The last action's message, shown as a toast; set through <see cref="SetStatus"/>.</summary>
    public string StatusText = "";

    /// <summary>Bumped on every message, so the same message twice toasts twice.</summary>
    public int StatusSerial;

    public void SetStatus(string message)
    {
        StatusText = message;
        StatusSerial++;
    }
}

/// <summary>Entities in the editor sidebar that flow around open dropdowns.</summary>
public struct EditorSidebarLayout(Entity panel) : IComponent
{
    public Entity Panel = panel;
}

/// <summary>
/// The note-key bar inside the left panel. It shows while placing notes or dash
/// notes, the two kinds that carry a key, and leaves the layout otherwise.
/// </summary>
public struct EditorNoteBarPanel : IComponent;

/// <summary>
/// The level info panel's song name. Changing the song is rare, so the name
/// and its CHANGE button live there rather than in the header.
/// </summary>
public struct EditorSongLabel : IComponent;

/// <summary>The left panel holding the current tool's settings.</summary>
public struct EditorLeftMenu(Entity panel) : IComponent
{
    public Entity Panel = panel;
}

/// <summary>
/// Binds the generic Xanadu.UI wheel to the editor's dash direction. The wheel
/// owns presentation and pointer handling; this adapter only maps wedge indices
/// to <see cref="DashDirection"/> and owns the floating widget's visibility.
/// </summary>
public struct EditorDirectionWheel(Entity wheel, Entity label)
    : IComponent
{
    public Entity Wheel = wheel;
    public Entity Label = label;
}
