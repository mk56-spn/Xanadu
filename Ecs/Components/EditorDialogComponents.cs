using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Levels;

namespace Xanadu.Ecs;

public enum EditorDialogKind
{
    /// <summary>Picks the song the level is timed to.</summary>
    Song,

    /// <summary>Offers to restore autosaved edits found when a level opened.</summary>
    Recovery,

    /// <summary>F10: the level's custom shader slots.</summary>
    Shaders,

    /// <summary>One of the map's colours; the argument is an <see cref="EditorColorTarget"/>.</summary>
    MapColor,
}

/// <summary>
/// A modal editor dialog, on its root: a full-screen scrim with the panel in its
/// middle, so nothing under it takes a press while it is open. Open one with
/// <see cref="EditorDialogs.Open"/>; <c>EditorDialogSystem</c> fills and runs it.
/// </summary>
public struct EditorDialog(EditorDialogKind kind) : IComponent
{
    public EditorDialogKind Kind = kind;

    /// <summary>Asks for it to open on the next update, with <see cref="Arg"/>.</summary>
    public bool OpenRequested;
    public int Arg;
}

public enum EditorDialogAction
{
    Close,

    /// <summary>A row of the song list; the argument is its index.</summary>
    PickSong,
    UseSong,

    Recover,
    Discard,

    /// <summary>A row of the shader list; the argument is its slot.</summary>
    PickShader,
    ImportShader,
    RemoveShader,
    ApplyShader,
    ClearShader,
    PlayerShader,
    FieldShader,
    PlayerShaderOff,
    FieldShaderOff,
}

/// <summary>A button in an editor dialog: what it does, and for a list row, which row.</summary>
public struct EditorDialogButton(EditorDialogAction action, int arg = 0) : IComponent
{
    public EditorDialogAction Action = action;
    public int Arg = arg;
}

/// <summary>The editor dialogs' parts and what each is showing, on the editor entity.</summary>
public struct EditorDialogs() : IComponent
{
    public Entity SongList;
    public Entity SongInfo;
    public Entity SongUse;
    public List<SongDocument> Songs = new();
    public int Song = -1;

    public Entity RecoveryMessage;

    public Entity ShaderList;
    public Entity ShaderInfo;

    /// <summary>The highlighted shader slot, from 1.</summary>
    public int Shader = 1;
    public float ShaderRefreshIn;

    public Entity ColorTitle;
    public Entity ColorHint;
    public Entity ColorPicker;
    public EditorColorTarget ColorTarget;
    public Godot.Color ColorShown;

    /// <summary>The dialog of a kind, or null when the editor has none.</summary>
    public static Entity Find(EntityStore store, EditorDialogKind kind)
    {
        foreach (Entity entity in store.Query<EditorDialog>().WithDisabled().ToEntityList())
        {
            if (entity.GetComponent<EditorDialog>().Kind == kind)
                return entity;
        }

        return default;
    }

    /// <summary>Asks for a dialog to open on the next update.</summary>
    public static void Open(EntityStore store, EditorDialogKind kind, int arg = 0)
    {
        Entity dialog = Find(store, kind);
        if (dialog.IsNull)
            return;

        ref EditorDialog state = ref dialog.GetComponent<EditorDialog>();
        state.OpenRequested = true;
        state.Arg = arg;
    }

    public static bool IsOpen(EntityStore store, EditorDialogKind kind)
    {
        Entity dialog = Find(store, kind);
        return !dialog.IsNull && (!UiLayout.IsHidden(dialog) || dialog.GetComponent<EditorDialog>().OpenRequested);
    }

    /// <summary>Whether any dialog is open or about to be, which takes the keyboard and the pointer.</summary>
    public static bool AnyOpen(EntityStore store)
    {
        foreach (Entity entity in store.Query<EditorDialog>().WithDisabled().ToEntityList())
        {
            if (!UiLayout.IsHidden(entity) || entity.GetComponent<EditorDialog>().OpenRequested)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Closes whichever dialog is open, as Escape or a CLOSE button does. The
    /// recovery offer stays: dropping autosaved edits takes an answer, not a key.
    /// </summary>
    public static void Dismiss(EntityStore store)
    {
        foreach (Entity entity in store.Query<EditorDialog>().WithDisabled().ToEntityList())
        {
            ref EditorDialog dialog = ref entity.GetComponent<EditorDialog>();
            if (dialog.Kind == EditorDialogKind.Recovery)
                continue;

            dialog.OpenRequested = false;
            UiLayout.SetHidden(entity, true);
        }
    }
}
