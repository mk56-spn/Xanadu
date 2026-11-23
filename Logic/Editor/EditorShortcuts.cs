using System;
using System.Collections.Generic;
using Xanadu.Core.Keys;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor.Intents;
using Xanadu.Model.Settings;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Timeline;

namespace Xanadu.Logic.Editor;

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
}

/// <summary>Every key binding in the level editor; the first live match wins.</summary>
public static class EditorShortcuts
{
    /// <summary>A chord that emits <paramref name="Intent"/> on the editor while one of <paramref name="Scope"/> is live.</summary>
    public readonly record struct Binding(KeyChord Chord, IIntent Intent, EditorShortcutScope Scope);

    public static readonly IReadOnlyList<Binding> All = Build();

    /// <summary>Which scopes are live, from most to least restrictive state.</summary>
    public static EditorShortcutScope ActiveScopes(
        bool dialog,
        bool help,
        bool warning,
        bool typing)
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

        return scopes | EditorShortcutScope.Field;
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
            new(new KeyChord(DebugKeys.Preview), new PreviewLevel(FromPlayhead: false), always),
            new(new KeyChord(DebugKeys.Preview, Shift: true), new PreviewLevel(FromPlayhead: true), always),
            new(new KeyChord(Key.Escape), new CloseEditorDialog(), EditorShortcutScope.Dialog),
            new(new KeyChord(GameDebugKeys.EditorHelp), new ToggleShortcutHelp(), always),
            new(new KeyChord(Key.F), new FitView(), notTyping),
            new(new KeyChord(Key.Escape), new ToggleShortcutHelp(), EditorShortcutScope.Help),
            new(new KeyChord(Key.S, Ctrl: true), new SaveLevel(), always),
            new(new KeyChord(Key.Z, Ctrl: true), new Undo(), always),
            new(new KeyChord(Key.Y, Ctrl: true), new Redo(), always),
            new(new KeyChord(Key.Z, Ctrl: true, Shift: true), new Redo(), always),
            new(new KeyChord(Key.C, Ctrl: true), new CopySelection(), notTyping),
            new(new KeyChord(Key.V, Ctrl: true), new PasteSelection(), notTyping),


            new(new KeyChord(Key.Escape), new LeaveEditor(), field),
            new(new KeyChord(Key.Enter), new GoForward(), field),
            new(new KeyChord(Key.KpEnter), new GoForward(), field),
            new(new KeyChord(Key.Delete), new DeleteSelection(), field),
            new(new KeyChord(Key.Backspace), new DeleteSelection(), field),
            new(new KeyChord(Key.Left), new MoveSelection(-1), field),
            new(new KeyChord(Key.Right), new MoveSelection(1), field),
        };

        foreach ((KeyChord chord, TimelineCommand command) in TimelineShortcuts.All)
            bindings.Add(new Binding(chord, new TimelineKey(command), field));

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
            bindings.Add(new Binding(new KeyChord(key), new PickPlacement(mode), EditorShortcutScope.Field));
        }
    }
}
