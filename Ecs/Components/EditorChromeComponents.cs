using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Settings;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Ui;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

/// <summary>The song's loudness by frequency band, for the editor timeline; null without a song.</summary>
public struct WaveformPreview(string path, WaveformData? data = null) : IComponent
{
    public string Path = path;
    public WaveformData? Data = data;
}

/// <summary>Screen-space region occupied by the editor's side controls.</summary>
public struct EditorSidebar(Rect2 bounds) : IComponent
{
    public Rect2 Bounds = bounds;
}

public enum EditorDropdownKind
{
    Snap,
    Character,
}


public enum EditorColorTarget
{
    Background,
    Outside,
    Frame,
}

/// <summary>
/// Part of a metadata colour row: the swatch that shows, and opens, one of the
/// map's colours, or the text beside it with the stored colour or DEFAULT.
/// </summary>
public struct EditorColorRow(EditorColorTarget target, bool hex) : IComponent
{
    public EditorColorTarget Target = target;
    public bool Hex = hex;
}

public enum EditorPanelKind
{
    /// <summary>The level metadata panel; it starts hidden.</summary>
    Metadata,
    /// <summary>The start-point property panel below the metadata.</summary>
    Start,
    /// <summary>The F9 editor-binding reference overlay.</summary>
    Bindings,
}

/// <summary>A floating editor panel, on the panel itself, with the place it opens at.</summary>
public struct EditorPanel(EditorPanelKind kind, Vector2 home) : IComponent
{
    public EditorPanelKind Kind = kind;
    public Vector2 Home = home;

    /// <summary>The panel of a kind, or null when there is none.</summary>
    public static Entity Find(EntityStore store, EditorPanelKind kind)
    {
        foreach (Entity entity in store.Query<EditorPanel>().WithDisabled().ToEntityList())
        {
            if (entity.GetComponent<EditorPanel>().Kind == kind)
                return entity;
        }

        return default;
    }
}

/// <summary>A native editor panel/dialog request raised by an ECS button.</summary>
public struct EditorUiRequest(EditorButtonKind kind = EditorButtonKind.None) : IComponent
{
    public EditorButtonKind Kind = kind;
}

/// <summary>Editor-specific binding for a generic dropdown.</summary>
public struct EditorDropdownControl(EditorDropdownKind kind) : IComponent
{
    public EditorDropdownKind Kind = kind;
}

/// <summary>Metronome toggle for the editor.</summary>
public struct EditorMetronome(bool enabled) : IComponent
{
    public bool Enabled = enabled;
}

/// <summary>Pending warning message to show in the editor warning popup.</summary>
public struct WarningRequest(string message) : IComponent
{
    public string Message = message;
}

/// <summary>State and child entities of the ECS warning popup.</summary>
public struct WarningPopup(Entity message, Entity button) : IComponent
{
    public Entity Message = message;
    public Entity Button = button;
}
