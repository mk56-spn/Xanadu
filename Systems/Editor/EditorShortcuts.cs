using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Input;
using Xanadu.Systems.Timeline;

namespace Xanadu.Systems.Editor;

/// <summary>What a key in the level editor can do.</summary>
public enum EditorShortcut
{
    Preview,
    PreviewHere,
    ToggleShaders,
    CloseDialog,
    ToggleHelp,
    FitView,
    Save,
    Undo,
    Redo,
    Copy,
    Paste,
    CloseTarget,
    ReopenTargetedGroup,
    CloseGroupLayer,
    Timeline,
    Place,
    Back,
    Forward,
    TapTempo,
    OpenSong,
    Delete,
    RotateSelection,
    FlipHorizontal,
    FlipVertical,
    CycleOneWay,
    MoveLeft,
    MoveRight,

    /// <summary>Closes the outline the polygon tool is laying into a shape.</summary>
    FinishPolygon,

    /// <summary>Drops the outline the polygon tool is laying.</summary>
    CancelPolygon,
}

/// <summary>When a binding can fire. <see cref="EditorShortcuts.ActiveScopes"/> says which are live.</summary>
[Flags]
public enum EditorShortcutScope
{
    None = 0,

    /// <summary>Unless a dialog is up, even while typing.</summary>
    Always = 1,

    /// <summary>Unless a dialog is up or a text field is in use.</summary>
    NotTyping = 2,

    /// <summary>The F9 binding list is open.</summary>
    Help = 4,

    /// <summary>An effect's move target is being dragged on the playfield.</summary>
    TargetEdit = 8,

    /// <summary>A group's overlay is up; it is modal, so <see cref="Field"/> is off.</summary>
    GroupOverlay = 16,

    /// <summary>Working on the playfield with nothing in the way.</summary>
    Field = 32,

    /// <summary>An editor dialog is up; it is modal, so only the keys that close it work.</summary>
    Dialog = 64,

    /// <summary>The polygon tool is laying an outline, which Enter and Esc finish before they do anything else.</summary>
    PolygonDraft = 128,
}

/// <summary>Every key binding in the level editor; the first live match wins.</summary>
public static class EditorShortcuts
{
    public readonly record struct Binding(
        KeyChord Chord,
        EditorShortcut Action,
        EditorShortcutScope Scope,
        EditorPlacementMode Placement = EditorPlacementMode.Notes,
        TimelineCommand Timeline = TimelineCommand.TogglePlay);

    public static readonly IReadOnlyList<Binding> All = Build();

    /// <summary>Which scopes are live, from most to least restrictive state.</summary>
    public static EditorShortcutScope ActiveScopes(
        bool dialog,
        bool help,
        bool warning,
        bool typing,
        bool targetEdit,
        bool groupOverlay,
        bool polygonDraft = false)
    {
        if (dialog)
            return EditorShortcutScope.Dialog;

        EditorShortcutScope scopes = EditorShortcutScope.Always;
        if (help)
            scopes |= EditorShortcutScope.Help;
        if (!typing)
            scopes |= EditorShortcutScope.NotTyping;
        if (warning)
            return scopes;
        if (targetEdit)
            scopes |= EditorShortcutScope.TargetEdit;
        if (typing)
            return scopes;

        if (groupOverlay)
            return scopes | EditorShortcutScope.GroupOverlay;
        scopes |= EditorShortcutScope.Field;
        return polygonDraft ? scopes | EditorShortcutScope.PolygonDraft : scopes;
    }

    public static Binding? Find(KeyChord chord, EditorShortcutScope scopes)
    {
        foreach (Binding binding in All)
        {
            if (binding.Chord == chord && (binding.Scope & scopes) != 0)
                return binding;
        }

        return null;
    }

    private static List<Binding> Build()
    {
        const EditorShortcutScope always = EditorShortcutScope.Always;
        const EditorShortcutScope notTyping = EditorShortcutScope.NotTyping;
        const EditorShortcutScope target = EditorShortcutScope.TargetEdit;
        const EditorShortcutScope group = EditorShortcutScope.GroupOverlay;
        const EditorShortcutScope field = EditorShortcutScope.Field;

        var bindings = new List<Binding>
        {
            new(new KeyChord(DebugKeys.Preview), EditorShortcut.Preview, always),
            new(new KeyChord(DebugKeys.Preview, Shift: true), EditorShortcut.PreviewHere, always),
            new(new KeyChord(DebugKeys.EditorShaders), EditorShortcut.ToggleShaders, always | EditorShortcutScope.Dialog),
            new(new KeyChord(Key.Escape), EditorShortcut.CloseDialog, EditorShortcutScope.Dialog),
            new(new KeyChord(DebugKeys.EditorHelp), EditorShortcut.ToggleHelp, always),
            new(new KeyChord(Key.F), EditorShortcut.FitView, notTyping),
            new(new KeyChord(Key.Escape), EditorShortcut.ToggleHelp, EditorShortcutScope.Help),
            new(new KeyChord(Key.S, Ctrl: true), EditorShortcut.Save, always),
            new(new KeyChord(Key.Z, Ctrl: true), EditorShortcut.Undo, always),
            new(new KeyChord(Key.Y, Ctrl: true), EditorShortcut.Redo, always),
            new(new KeyChord(Key.Z, Ctrl: true, Shift: true), EditorShortcut.Redo, always),
            new(new KeyChord(Key.C, Ctrl: true), EditorShortcut.Copy, notTyping),
            new(new KeyChord(Key.V, Ctrl: true), EditorShortcut.Paste, notTyping),

            new(new KeyChord(Key.Escape), EditorShortcut.CloseTarget, target),
            new(new KeyChord(Key.T), EditorShortcut.ReopenTargetedGroup, target),

            new(new KeyChord(Key.Escape), EditorShortcut.CloseGroupLayer, group),
            new(new KeyChord(Key.Space), EditorShortcut.Timeline, group, Timeline: TimelineCommand.TogglePlay),

            new(new KeyChord(Key.Escape), EditorShortcut.CancelPolygon, EditorShortcutScope.PolygonDraft),
            new(new KeyChord(Key.Enter), EditorShortcut.FinishPolygon, EditorShortcutScope.PolygonDraft),
            new(new KeyChord(Key.KpEnter), EditorShortcut.FinishPolygon, EditorShortcutScope.PolygonDraft),

            new(new KeyChord(Key.Escape), EditorShortcut.Back, field),
            new(new KeyChord(Key.Enter), EditorShortcut.Forward, field),
            new(new KeyChord(Key.KpEnter), EditorShortcut.Forward, field),
            new(new KeyChord(Key.T), EditorShortcut.TapTempo, field),
            new(new KeyChord(Key.L), EditorShortcut.OpenSong, field),
            new(new KeyChord(Key.R), EditorShortcut.RotateSelection, field),
            new(new KeyChord(Key.H), EditorShortcut.FlipHorizontal, field),
            new(new KeyChord(Key.V), EditorShortcut.FlipVertical, field),
            new(new KeyChord(Key.O), EditorShortcut.CycleOneWay, field),
            new(new KeyChord(Key.Delete), EditorShortcut.Delete, field),
            new(new KeyChord(Key.Backspace), EditorShortcut.Delete, field),
            new(new KeyChord(Key.Left), EditorShortcut.MoveLeft, field),
            new(new KeyChord(Key.Right), EditorShortcut.MoveRight, field),
        };

        foreach ((KeyChord chord, TimelineCommand command) in TimelineShortcuts.All)
            bindings.Add(new Binding(chord, EditorShortcut.Timeline, field, Timeline: command));

        for (Key key = Key.Key0; key <= Key.Key9; key++)
            AddPlacement(bindings, key);
        for (Key key = Key.Kp0; key <= Key.Kp9; key++)
            AddPlacement(bindings, key);

        return bindings;
    }

    private static void AddPlacement(List<Binding> bindings, Key key)
    {
        if (EditorPlacementKeybinds.TryGet(key, out EditorPlacementMode mode))
        {
            bindings.Add(new Binding(
                new KeyChord(key),
                EditorShortcut.Place,
                EditorShortcutScope.Field,
                mode));
        }
    }
}

/// <summary>What runs an editor shortcut: the overlay, which owns the dialogs and the status line.</summary>
public interface IEditorShortcutTarget
{
    /// <summary>False once the overlay has closed, so keys go back to the scene.</summary>
    bool IsOpen { get; }

    EditorShortcutScope ActiveScopes { get; }

    void Run(EditorShortcuts.Binding binding);
}

/// <summary>Marks the editor entity with whatever runs its shortcuts.</summary>
public struct EditorShortcutHost(IEditorShortcutTarget target) : Friflo.Engine.ECS.IComponent
{
    public IEditorShortcutTarget Target = target;
}
