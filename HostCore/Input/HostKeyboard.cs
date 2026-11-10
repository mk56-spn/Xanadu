using System.Collections.Generic;
using MoonWorks.Input;
using Xanadu.Systems.Input;

namespace Xanadu.Host.Input;

/// <summary>
/// The game's keyboard (Godot <see cref="Key"/> values) read from MoonWorks.
/// "Pressed" here is Godot's meaning: held down this frame.
/// </summary>
internal sealed class HostKeyboard(Keyboard keyboard) : IKeyboardState
{
    public bool IsPressed(Key key) => key switch
    {
        Key.Shift => keyboard.IsDown(KeyCode.LeftShift) || keyboard.IsDown(KeyCode.RightShift),
        Key.Ctrl => keyboard.IsDown(KeyCode.LeftControl) || keyboard.IsDown(KeyCode.RightControl),
        Key.Alt => keyboard.IsDown(KeyCode.LeftAlt) || keyboard.IsDown(KeyCode.RightAlt),
        Key.Meta => keyboard.IsDown(KeyCode.LGui) || keyboard.IsDown(KeyCode.RGui),
        _ => ToKeyCode(key) is { } code && keyboard.IsDown(code),
    };

    /// <summary>Feeds this frame's new presses to the shortcut queue, the way Godot's unhandled key events did.</summary>
    /// <param name="allow">Which keys may reach the queue this frame; null for all.</param>
    public void FeedPresses(Keyboard keyboard, KeyPressQueue queue, System.Func<Key, bool>? allow = null)
    {
        bool ctrl = IsPressed(Key.Ctrl) || IsPressed(Key.Meta);
        bool shift = IsPressed(Key.Shift);
        foreach ((Key key, KeyCode code) in Pairs)
            if (keyboard.IsPressed(code) && (allow is null || allow(key)))
                queue.Press(new KeyChord(key, ctrl, shift));
    }

    /// <summary>SDL keycode for a Godot key: letters are lower case in SDL, specials have their own table.</summary>
    public static KeyCode? ToKeyCode(Key key)
    {
        long value = (long)key;
        if (value is >= 'A' and <= 'Z')
            return (KeyCode)(value + ('a' - 'A'));
        if (value is >= 32 and < 127)
            return (KeyCode)value;
        return Specials.TryGetValue(key, out KeyCode code) ? code : null;
    }

    private static readonly Dictionary<Key, KeyCode> Specials = new()
    {
        [Key.Escape] = KeyCode.Escape,
        [Key.Tab] = KeyCode.Tab,
        [Key.Backspace] = KeyCode.Backspace,
        [Key.Enter] = KeyCode.Return,
        [Key.KpEnter] = KeyCode.KeypadEnter,
        [Key.Insert] = KeyCode.Insert,
        [Key.Delete] = KeyCode.Delete,
        [Key.Home] = KeyCode.Home,
        [Key.End] = KeyCode.End,
        [Key.Pageup] = KeyCode.PageUp,
        [Key.Pagedown] = KeyCode.PageDown,
        [Key.Left] = KeyCode.Left,
        [Key.Right] = KeyCode.Right,
        [Key.Up] = KeyCode.Up,
        [Key.Down] = KeyCode.Down,
        [Key.Capslock] = KeyCode.CapsLock,
        [Key.F1] = KeyCode.F1, [Key.F2] = KeyCode.F2, [Key.F3] = KeyCode.F3, [Key.F4] = KeyCode.F4,
        [Key.F5] = KeyCode.F5, [Key.F6] = KeyCode.F6, [Key.F7] = KeyCode.F7, [Key.F8] = KeyCode.F8,
        [Key.F9] = KeyCode.F9, [Key.F10] = KeyCode.F10, [Key.F11] = KeyCode.F11, [Key.F12] = KeyCode.F12,
    };

    private static readonly List<(Key, KeyCode)> Pairs = BuildPairs();

    private static List<(Key, KeyCode)> BuildPairs()
    {
        var pairs = new List<(Key, KeyCode)>();
        for (long c = 32; c < 127; c++)
        {
            if (c is >= 'a' and <= 'z')
                continue;
            var key = (Key)c;
            if (ToKeyCode(key) is { } code)
                pairs.Add((key, code));
        }
        foreach ((Key key, KeyCode code) in Specials)
            pairs.Add((key, code));
        return pairs;
    }
}
