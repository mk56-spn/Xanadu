using Friflo.Engine.ECS;

namespace Xanadu.Core;

/// <summary>
/// The entity a control (a toggle, a row, a field, a button) acts on or shows.
/// One link type for every control of every editor; the other way round,
/// <c>thing.GetIncomingLinks&lt;Subject&gt;()</c> lists the controls about a thing.
/// </summary>
public struct Subject(Entity target) : ILinkComponent
{
    public Entity Target = target;

    public readonly Entity GetIndexedValue() => Target;
}

/// <summary>
/// The tags a switch turns on and off on its <see cref="Subject"/>: a toggle
/// stores which state it flips as the tags themselves, not as an enum naming them.
/// </summary>
public struct Switches(Tags tags) : IComponent
{
    public Tags Tags = tags;

    /// <summary>The value the switch and its subject last agreed on, so a change on either side is told apart.</summary>
    public bool Synced;
}
