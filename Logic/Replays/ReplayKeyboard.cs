using System.Collections.Generic;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Logic.Replays;

/// <summary>
/// The keyboard the playfield reads. It passes the real keyboard through
/// until a replay takes over, then answers only with the replayed keys, so
/// the play systems cannot tell the difference.
/// </summary>
public sealed class ReplayKeyboard(IKeyboardState real) : IKeyboardState
{
    private readonly HashSet<Key> _replayed = [];
    private readonly Dictionary<Key, long> _changedAt = [];

    /// <summary>The real keyboard, which the recorder samples directly.</summary>
    public IKeyboardState Real { get; } = real;

    public bool IsReplaying { get; private set; }

    public bool IsPressed(Key key) =>
        IsReplaying ? _replayed.Contains(key) : Real.IsPressed(key);

    public long? ChangedAt(Key key) =>
        IsReplaying ? _changedAt.TryGetValue(key, out long ticks) ? ticks : null : Real.ChangedAt(key);

    /// <summary>Holds <paramref name="keys"/>; <paramref name="changedAt"/> says when each last changed (Stopwatch ticks), where known.</summary>
    public void Replay(IEnumerable<Key> keys, IReadOnlyDictionary<Key, long>? changedAt = null)
    {
        IsReplaying = true;
        _replayed.Clear();
        foreach (Key key in keys)
            _replayed.Add(key);
        _changedAt.Clear();
        if (changedAt is not null)
        {
            foreach ((Key key, long ticks) in changedAt)
                _changedAt[key] = ticks;
        }
    }

    public void StopReplay()
    {
        IsReplaying = false;
        _replayed.Clear();
        _changedAt.Clear();
    }
}
