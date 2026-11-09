using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Ecs.Storage;
using Xanadu.Systems.Input;

namespace Xanadu.Settings;

/// <summary>
/// The seven playfield keys: four directions that steer directional notes and
/// three action keys that hit action notes.
/// </summary>
public enum KeybindAction
{
    Left,
    Right,
    Up,
    Down,
    Action1,
    Action2,
    Action3,
}

/// <summary>Persisted key bindings with capture, conflict swapping, and defaults.</summary>
public sealed class KeybindSettings
{
    public static readonly KeybindAction[] Actions =
    {
        KeybindAction.Left,
        KeybindAction.Right,
        KeybindAction.Up,
        KeybindAction.Down,
        KeybindAction.Action1,
        KeybindAction.Action2,
        KeybindAction.Action3,
    };

    /// <summary>The three action keys, in bar order.</summary>
    public static readonly KeybindAction[] ActionKeys =
    {
        KeybindAction.Action1,
        KeybindAction.Action2,
        KeybindAction.Action3,
    };

    /// <summary>Whether the key steers rather than hits.</summary>
    public static bool IsDirection(KeybindAction action) => action is
        KeybindAction.Left or KeybindAction.Right or KeybindAction.Up or KeybindAction.Down;

    private const string DefaultPath = "user://keybinds.json";

    private readonly Dictionary<KeybindAction, Key> _keys = new();
    private readonly PersistedFile<Dictionary<string, int>> _file;

    public KeybindSettings(string? path = null, bool persist = true)
    {
        _file = new PersistedFile<Dictionary<string, int>>(path, DefaultPath, persist);

        foreach (KeybindAction action in Actions)
            _keys[action] = DefaultFor(action);

        if (persist)
            Load();
    }

    public KeybindAction? Capturing { get; private set; }

    public Key Get(KeybindAction action) =>
        _keys.TryGetValue(action, out Key key) ? key : DefaultFor(action);

    public void BeginCapture(KeybindAction action) => Capturing = action;

    public void CancelCapture() => Capturing = null;

    /// <summary>
    /// Consumes a key while capturing. Escape cancels; the function keys are
    /// kept for the debug bands, so binding one leaves the capture open.
    /// </summary>
    public bool TryCapture(Key key)
    {
        if (Capturing is not { } action)
            return false;

        if (key == Key.Escape || key == Key.None)
        {
            Capturing = null;
            return true;
        }

        if (IsModifier(key) || DebugKeys.IsReserved(key))
            return true;

        Key oldKey = Get(action);
        KeybindAction? conflict = null;

        foreach (KeyValuePair<KeybindAction, Key> pair in _keys)
        {
            if (pair.Key != action && pair.Value == key)
            {
                conflict = pair.Key;
                break;
            }
        }

        if (conflict is { } other)
            _keys[other] = oldKey;

        _keys[action] = key;
        Capturing = null;
        Save();
        return true;
    }

    public static Key DefaultFor(KeybindAction action) => action switch
    {
        KeybindAction.Left => Key.S,
        KeybindAction.Right => Key.D,
        KeybindAction.Up => Key.K,
        KeybindAction.Down => Key.L,
        KeybindAction.Action1 => Key.F,
        KeybindAction.Action2 => Key.Space,
        KeybindAction.Action3 => Key.J,
        _ => Key.None,
    };

    public static string LabelFor(KeybindAction action) => action switch
    {
        KeybindAction.Left => "LEFT",
        KeybindAction.Right => "RIGHT",
        KeybindAction.Up => "UP",
        KeybindAction.Down => "DOWN",
        KeybindAction.Action1 => "ACTION 1",
        KeybindAction.Action2 => "ACTION 2",
        KeybindAction.Action3 => "ACTION 3",
        _ => "",
    };

    public static string KeyLabel(Key key) => key switch
    {
        Key.None => "—",
        Key.Space => "SPACE",
        _ => key.ToString().ToUpperInvariant(),
    };

    private static bool IsModifier(Key key) => key is
        Key.Shift or
        Key.Ctrl or
        Key.Alt or
        Key.Meta or
        Key.Capslock;

    private void Load()
    {
        Dictionary<string, int>? values = _file.Load();
        if (values is null)
            return;

        foreach (KeybindAction action in Actions)
        {
            if (values.TryGetValue(action.ToString(), out int raw) &&
                Enum.IsDefined(typeof(Key), (long)raw))
            {
                _keys[action] = (Key)raw;
            }
        }
    }

    private void Save()
    {
        var values = new Dictionary<string, int>();

        foreach (KeyValuePair<KeybindAction, Key> pair in _keys)
            values[pair.Key.ToString()] = (int)pair.Value;

        _file.Save(values);
    }
}
