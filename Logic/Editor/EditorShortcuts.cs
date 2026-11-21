using Xanadu.Model.Settings;
using System;
using System.Collections.Generic;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Timeline;

namespace Xanadu.Logic.Editor;

/// <summary>What a key in the level editor can do.</summary>
public enum EditorShortcut
{
    Preview,
    PreviewHere,
    CloseDialog,
    ToggleHelp,
    FitView,
    Save,
    Undo,
    Redo,
    Copy,
    Paste,
    Timeline,
    Place,
    Back,
    Forward,
    Delete,
    MoveLeft,
    MoveRight,

    /// <summary>Finishes the route or hold being laid, ending it at the playhead (as right click does).</summary>
    FinishDraft,

    /// <summary>Drops the route or hold being laid.</summary>
    CancelDraft,
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

    /// <summary>Working on the playfield with nothing in the way.</summary>
    Field = 32,

    /// <summary>An editor dialog is up; it is modal, so only the keys that close it work.</summary>
    Dialog = 64,

    /// <summary>A route or hold is being laid, which Enter and Esc finish before they do anything else.</summary>
    Draft = 128,
}

/// <summary>Every key binding in the level editor; the first live match wins.</summary>
public static class EditorShortcuts
{
    public readonly record struct Binding(
        KeyChord Chord,
        EditorShortcut Action,
        EditorShortcutScope Scope,
        EditorPlacementMode Placement = EditorPlacementMode.Curve,
        TimelineCommand Timeline = TimelineCommand.TogglePlay);

    public static readonly IReadOnlyList<Binding> All = Build();

    /// <summary>Which scopes are live, from most to least restrictive state.</summary>
    public static EditorShortcutScope ActiveScopes(
        bool dialog,
        bool help,
        bool warning,
        bool typing,
        bool drafting = false)
    {
        if (dialog)
            return EditorShortcutScope.Dialog;

        EditorShortcutScope scopes = EditorShortcutScope.Always;
        if (help)
            scopes |= EditorShortcutScope.Help;
        if (!typing)
            scopes |= EditorShortcutScope.NotTyping;
        if (warning || typing)
            return scopes;

        scopes |= EditorShortcutScope.Field;
        return drafting ? scopes | EditorShortcutScope.Draft : scopes;
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
        const EditorShortcutScope field = EditorShortcutScope.Field;

        var bindings = new List<Binding>
        {
            new(new KeyChord(DebugKeys.Preview), EditorShortcut.Preview, always),
            new(new KeyChord(DebugKeys.Preview, Shift: true), EditorShortcut.PreviewHere, always),
            new(new KeyChord(Key.Escape), EditorShortcut.CloseDialog, EditorShortcutScope.Dialog),
            new(new KeyChord(GameDebugKeys.EditorHelp), EditorShortcut.ToggleHelp, always),
            new(new KeyChord(Key.F), EditorShortcut.FitView, notTyping),
            new(new KeyChord(Key.Escape), EditorShortcut.ToggleHelp, EditorShortcutScope.Help),
            new(new KeyChord(Key.S, Ctrl: true), EditorShortcut.Save, always),
            new(new KeyChord(Key.Z, Ctrl: true), EditorShortcut.Undo, always),
            new(new KeyChord(Key.Y, Ctrl: true), EditorShortcut.Redo, always),
            new(new KeyChord(Key.Z, Ctrl: true, Shift: true), EditorShortcut.Redo, always),
            new(new KeyChord(Key.C, Ctrl: true), EditorShortcut.Copy, notTyping),
            new(new KeyChord(Key.V, Ctrl: true), EditorShortcut.Paste, notTyping),

            new(new KeyChord(Key.Escape), EditorShortcut.CancelDraft, EditorShortcutScope.Draft),
            new(new KeyChord(Key.Enter), EditorShortcut.FinishDraft, EditorShortcutScope.Draft),
            new(new KeyChord(Key.KpEnter), EditorShortcut.FinishDraft, EditorShortcutScope.Draft),

            new(new KeyChord(Key.Escape), EditorShortcut.Back, field),
            new(new KeyChord(Key.Enter), EditorShortcut.Forward, field),
            new(new KeyChord(Key.KpEnter), EditorShortcut.Forward, field),
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
