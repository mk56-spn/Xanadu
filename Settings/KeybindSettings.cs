using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Ecs.Storage;

namespace Xanadu.Settings;

/// <summary>The seven playfield key bindings.</summary>
public enum KeybindAction
{
    L1,
    L2,
    L3,
    M,
    R1,
    R2,
    R3,
}

/// <summary>Persisted key bindings with capture, conflict swapping, and defaults.</summary>
public sealed class KeybindSettings
{
    public static readonly KeybindAction[] Actions =
    {
        KeybindAction.L1,
        KeybindAction.L2,
        KeybindAction.L3,
        KeybindAction.M,
        KeybindAction.R1,
        KeybindAction.R2,
        KeybindAction.R3,
    };

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

    /// <summary>Consumes a key while capturing. Escape cancels.</summary>
    public bool TryCapture(Key key)
    {
        if (Capturing is not { } action)
            return false;

        if (key == Key.Escape || key == Key.None)
        {
            Capturing = null;
            return true;
        }

        if (IsModifier(key))
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
        KeybindAction.L1 => Key.S,
        KeybindAction.L2 => Key.D,
        KeybindAction.L3 => Key.F,
        KeybindAction.M => Key.Space,
        KeybindAction.R1 => Key.J,
        KeybindAction.R2 => Key.K,
        KeybindAction.R3 => Key.L,
        _ => Key.None,
    };

    public static string LabelFor(KeybindAction action) => action switch
    {
        KeybindAction.L1 => "L1",
        KeybindAction.L2 => "L2",
        KeybindAction.L3 => "L3",
        KeybindAction.M => "M",
        KeybindAction.R1 => "R1",
        KeybindAction.R2 => "R2",
        KeybindAction.R3 => "R3",
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
