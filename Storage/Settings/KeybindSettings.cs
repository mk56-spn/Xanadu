using Xanadu.Model.Settings;
using System;
using System.Collections.Generic;
using Xanadu.Core.Maths;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Storage.Settings;

/// <summary>Persisted key bindings with capture, conflict swapping, and defaults.</summary>
public sealed class KeybindSettings
{
    private const string DefaultPath = "user://keybinds.json";

    private readonly Dictionary<KeybindAction, Key> _keys = new();
    private readonly PersistedFile<Dictionary<string, int>> _file;

    public KeybindSettings(string? path = null, bool persist = true)
    {
        _file = new PersistedFile<Dictionary<string, int>>(path, DefaultPath, persist);

        foreach (KeybindAction action in KeybindActions.All)
            _keys[action] = KeybindActions.DefaultFor(action);

        if (persist)
            Load();
    }

    public KeybindAction? Capturing { get; private set; }

    public Key Get(KeybindAction action) =>
        _keys.TryGetValue(action, out Key key) ? key : KeybindActions.DefaultFor(action);

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

        foreach (KeybindAction action in KeybindActions.All)
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
