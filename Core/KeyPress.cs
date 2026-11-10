using System.Collections.Generic;

namespace Xanadu.Ecs;

/// <summary>
/// Turns a held key into a single press event: the first frame the key reads
/// down counts, later frames do not, and releasing re-arms it. Every panel that
/// acts on the start of a press rather than holding it can share this.
/// </summary>
public sealed class KeyPress
{
    private readonly HashSet<Key> _held = new();

    /// <summary>True on the one frame <paramref name="key"/> starts being held.</summary>
    public bool Started(Key key, bool pressed)
    {
        bool wasHeld = !_held.Add(key);
        if (!pressed)
            _held.Remove(key);

        return pressed && !wasHeld;
    }
}
