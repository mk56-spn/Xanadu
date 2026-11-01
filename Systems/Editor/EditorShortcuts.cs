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
    ToggleShaders,
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
    MoveLeft,
    MoveRight,
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

    /// <summary>The F2 binding list is open.</summary>
    Help = 4,

    /// <summary>An effect's move target is being dragged on the playfield.</summary>
    TargetEdit = 8,

    /// <summary>A group's overlay is up; it is modal, so <see cref="Field"/> is off.</summary>
    GroupOverlay = 16,

    /// <summary>Working on the playfield with nothing in the way.</summary>
    Field = 32,
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
        bool groupOverlay)
    {
        if (dialog)
            return EditorShortcutScope.None;

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

        return scopes | (groupOverlay ? EditorShortcutScope.GroupOverlay : EditorShortcutScope.Field);
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
            new(new KeyChord(Key.F5), EditorShortcut.Preview, always),
            new(new KeyChord(Key.F9), EditorShortcut.ToggleShaders, always),
            new(new KeyChord(Key.F2), EditorShortcut.ToggleHelp, always),
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

            new(new KeyChord(Key.Escape), EditorShortcut.Back, field),
            new(new KeyChord(Key.Enter), EditorShortcut.Forward, field),
            new(new KeyChord(Key.KpEnter), EditorShortcut.Forward, field),
            new(new KeyChord(Key.T), EditorShortcut.TapTempo, field),
            new(new KeyChord(Key.L), EditorShortcut.OpenSong, field),
            new(new KeyChord(Key.Delete), EditorShortcut.Delete, field),
            new(new KeyChord(Key.Backspace), EditorShortcut.Delete, field),
            new(new KeyChord(Key.Left), EditorShortcut.MoveLeft, field),
            new(new KeyChord(Key.Right), EditorShortcut.MoveRight, field),
        };

        foreach ((KeyChord chord, TimelineCommand command) in TimelineShortcuts.All)
            bindings.Add(new Binding(chord, EditorShortcut.Timeline, field, Timeline: command));

        for (Key key = Key.Key1; key <= Key.Key9; key++)
            AddPlacement(bindings, key);
        for (Key key = Key.Kp1; key <= Key.Kp9; key++)
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
