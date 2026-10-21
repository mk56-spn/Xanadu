using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace Writer.Settings;

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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly Dictionary<KeybindAction, Key> _keys = new();
    private readonly string _path;
    private readonly bool _persist;

    public KeybindSettings(string? path = null, bool persist = true)
    {
        _path = path ?? DefaultPath;
        _persist = persist;

        foreach (KeybindAction action in Actions)
            _keys[action] = DefaultFor(action);

        if (_persist)
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
        try
        {
            string path = ResolvePath();
            if (!File.Exists(path))
                return;

            Dictionary<string, int>? values =
                JsonSerializer.Deserialize<Dictionary<string, int>>(
                    File.ReadAllText(path),
                    JsonOptions);

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
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            GD.PushWarning($"Could not load keybinds: {exception.Message}");
        }
    }

    private void Save()
    {
        if (!_persist)
            return;

        try
        {
            var values = new Dictionary<string, int>();

            foreach (KeyValuePair<KeybindAction, Key> pair in _keys)
                values[pair.Key.ToString()] = (int)pair.Value;

            string path = ResolvePath();
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(
                path,
                JsonSerializer.Serialize(values, JsonOptions));
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            GD.PushWarning($"Could not save keybinds: {exception.Message}");
        }
    }

    private string ResolvePath() =>
        _path.StartsWith("user://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(_path)
            : _path;
}
