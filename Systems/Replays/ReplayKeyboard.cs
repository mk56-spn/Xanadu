using System.Collections.Generic;
using Godot;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Replays;

/// <summary>
/// The keyboard the playfield reads. It passes the real keyboard through
/// until a replay takes over, then answers only with the replayed keys, so
/// the play systems cannot tell the difference.
/// </summary>
public sealed class ReplayKeyboard(IKeyboardState real) : IKeyboardState
{
    private readonly HashSet<Key> _replayed = new();

    /// <summary>The real keyboard, which the recorder samples directly.</summary>
    public IKeyboardState Real { get; } = real;

    public bool IsReplaying { get; private set; }

    public bool IsPressed(Key key) =>
        IsReplaying ? _replayed.Contains(key) : Real.IsPressed(key);

    public void Replay(IEnumerable<Key> keys)
    {
        IsReplaying = true;
        _replayed.Clear();
        foreach (Key key in keys)
            _replayed.Add(key);
    }

    public void StopReplay()
    {
        IsReplaying = false;
        _replayed.Clear();
    }
}
