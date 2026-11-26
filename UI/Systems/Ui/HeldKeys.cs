using System.Collections.Generic;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// The keys a text editor watches, read once a frame: which went down this frame (<see cref="Pressed"/>) and which fire
/// again while held (<see cref="Fires"/>), after a delay and then steadily.
/// </summary>
internal sealed class HeldKeys(IKeyboardState keyboard, Key[] watched)
{
    private const float RepeatDelay = 0.4f, RepeatEvery = 0.033f;

    /// <summary>Keys down last frame, for presses; how long each repeating key has been held.</summary>
    private readonly HashSet<Key> _down = [];
    private readonly Dictionary<Key, float> _held = [];

    /// <summary>The watched keys that went down this frame.</summary>
    public HashSet<Key> Pressed { get; } = [];

    public void Read()
    {
        Pressed.Clear();
        foreach (Key key in watched)
        {
            if (keyboard.IsPressed(key))
            {
                if (_down.Add(key))
                    Pressed.Add(key);
            }
            else
                _down.Remove(key);
        }
    }

    /// <summary>A key fires when pressed, then repeatedly once held past a delay.</summary>
    public bool Fires(Key key, float delta)
    {
        if (Pressed.Contains(key))
        {
            _held[key] = 0;
            return true;
        }

        if (!_down.Contains(key) || !_held.TryGetValue(key, out float before))
        {
            _held.Remove(key);
            return false;
        }

        float now = before + delta;
        _held[key] = now;
        if (now < RepeatDelay)
            return false;
        return before < RepeatDelay || (int)((before - RepeatDelay) / RepeatEvery) != (int)((now - RepeatDelay) / RepeatEvery);
    }
}
